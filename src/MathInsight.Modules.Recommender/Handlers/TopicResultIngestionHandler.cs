using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Recommender.Persistence;
using MathInsight.Modules.Recommender.Persistence.Entities;
using MathInsight.Modules.Recommender.Services;
using MathInsight.Shared.Events;

namespace MathInsight.Modules.Recommender.Handlers;

/// <summary>
/// MediatR in-process handler for GradeCalculatedEvent.
/// Triggered by Grading module (004) after a TestSession is graded.
///
/// Responsibilities per spec:
///   RCM-08 — Idempotency: insert StudentTopicSessionResult only once per (session_id, tag_id).
///   RCM-05 — ExamAnchor: Exponential Decay weighted average over k≤5 recent sessions (β=0.8).
///   RCM-04 — OfficialPoint: 0.7 × ExamAnchor + 0.3 × PracticePoint.
///   RCM-07 — RecommendedDifficultyLevel: derived from OfficialPoint.
///   RCM-13 — MasteryStatus thresholds: NotLearned / Learning / Mastered.
///   U3     — Lazy-create TagsMastery with neutral OfficialPoint=5.00 when no prior row exists.
///   G1     — Trigger CompetencyEngine to recalculate CompetencyPoint after mastery update.
/// </summary>
public sealed class TopicResultIngestionHandler : INotificationHandler<GradeCalculatedEvent>
{
    // Exponential decay factor β (Ebbinghaus Forgetting Curve, RCM-05)
    private const decimal Beta = 0.8m;

    // Maximum exam history window (RCM-05)
    private const int MaxExamHistory = 5;

    // Mastery status threshold (RCM-13)
    private const decimal MasteredThreshold = 7.50m;

    private readonly RecommenderDbContext _db;
    private readonly ICompetencyEngine _competencyEngine;

    public TopicResultIngestionHandler(RecommenderDbContext db, ICompetencyEngine competencyEngine)
    {
        _db = db;
        _competencyEngine = competencyEngine;
    }

    public async Task Handle(GradeCalculatedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.PerTagResults.Count == 0)
            return;

        // A missing grade must not create an invalid grade-0/default competency row.
        var student = await _db.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.StudentId == notification.StudentId, cancellationToken);

        // PerTagResults.TagId is a TagTopic (topic tag) ID, not a TagDifficulty ID.
        // WeakTag evaluation is always per-topic. Difficulty is derived separately
        // via RecommendedDifficultyLevel → TagDifficulty.LevelValue → DifficultyID.
        foreach (var tagResult in notification.PerTagResults)
        {
            await IngestTopicResultAsync(notification, tagResult, cancellationToken);
        }

        // RCM-12 / G1: Recalculate CompetencyPoint once after all tags are updated.
        if (student?.CurrentGrade is int grade)
            await _competencyEngine.RecalculateAsync(notification.StudentId, grade, cancellationToken);
    }

    private async Task IngestTopicResultAsync(
        GradeCalculatedEvent evt,
        TopicGradeResult tagResult,
        CancellationToken ct)
    {
        var incomingRevision = Math.Max(1, evt.GradeRevision);

        // ── RCM-08: Idempotency ──────────────────────────────────────────────────
        // Skip if (session_id, tag_id) already exists — safe to call multiple times.
        var existingResult = await _db.StudentTopicSessionResults
            .FirstOrDefaultAsync(r => r.SessionId == evt.SessionId && r.TagId == tagResult.TagId, ct);

        if (existingResult is not null && existingResult.GradeRevision >= incomingRevision)
            return;

        // ── U3 / RCM no-history: Lazy-create TagsMastery if absent ───────────────
        var mastery = await _db.TagsMasteries
            .FirstOrDefaultAsync(tm => tm.StudentId == evt.StudentId && tm.TagId == tagResult.TagId, ct);

        if (mastery is null)
        {
            mastery = new TagsMastery
            {
                TagsMasteryId = Guid.NewGuid().ToString(),
                StudentId = evt.StudentId,
                TagId = tagResult.TagId,
                OfficialPoint = 5.00m,   // neutral baseline (RCM spec U3)
                PracticePoint = 5.00m,
                ExamAnchor = 5.00m,
                MasteryStatus = "NotLearned",
                NumberDone = 0,
                SeriesAnswerCount = 0,
                ExamHistory = "[]"
            };
            _db.TagsMasteries.Add(mastery);
        }

        if (string.Equals(evt.TestFormat, "Exam", StringComparison.OrdinalIgnoreCase))
        {
            // ── RCM-05: Update ExamAnchor using Exponential Decay ───────────────────
            var history = DeserializeHistory(mastery.ExamHistory);
            var existingHistoryIndex = history.FindIndex(item =>
                string.Equals(item.SessionId, evt.SessionId, StringComparison.OrdinalIgnoreCase));
            var hasUsableEvidence = tagResult.TotalItems > 0m && tagResult.MaxPoints > 0m;
            if (!hasUsableEvidence && existingHistoryIndex >= 0)
                history.RemoveAt(existingHistoryIndex);
            else if (hasUsableEvidence && existingHistoryIndex >= 0)
                history[existingHistoryIndex] = history[existingHistoryIndex] with
                {
                    GradeRevision = incomingRevision,
                    TopicScore = tagResult.TopicScore,
                    GradedAt = evt.GradedAt
                };
            else if (hasUsableEvidence)
                history.Insert(0, new ExamHistoryEntry(
                    evt.SessionId,
                    incomingRevision,
                    tagResult.TopicScore,
                    evt.GradedAt));
            if (history.Count > MaxExamHistory)
                history.RemoveAt(history.Count - 1); // drop oldest

            mastery.ExamAnchor = CalculateExamAnchor(history);
            mastery.ExamHistory = JsonSerializer.Serialize(history);

            // ── RCM-04: Recalculate OfficialPoint ────────────────────────────────────
            mastery.OfficialPoint = Math.Clamp(
                0.7m * mastery.ExamAnchor + 0.3m * mastery.PracticePoint,
                0.00m, 10.00m);
        }
        else if (string.Equals(evt.TestFormat, "Practice", StringComparison.OrdinalIgnoreCase))
        {
            mastery.LastPracticedTime = evt.GradedAt;
            // ── RCM-06: Update PracticePoint using Elo formula sequentially ─────────
            var tagAnswers = (evt.Answers ?? Array.Empty<GradedAnswerDto>())
                .Where(a => a.TagId == tagResult.TagId)
                .OrderBy(a => a.QuestionNo)
                .ToList();

            foreach (var ans in tagAnswers)
            {
                if (existingResult is not null && !ans.IsScoreInvalidated)
                    continue;

                decimal delta;
                if (existingResult is not null &&
                    ans.IsScoreInvalidated &&
                    ans.MachineIsCorrect is bool machineIsCorrect)
                {
                    delta = -CalculatePracticeDelta(machineIsCorrect, ans);
                    mastery.SeriesAnswerCount = Math.Max(0, mastery.SeriesAnswerCount - 1);
                }
                else
                {
                    delta = CalculatePracticeDelta(ans.IsCorrect, ans);
                    mastery.SeriesAnswerCount++;
                }

                mastery.PracticePoint = Math.Clamp(mastery.PracticePoint + delta, 0.00m, 10.00m);

                mastery.OfficialPoint = Math.Clamp(
                    0.7m * mastery.ExamAnchor + 0.3m * mastery.PracticePoint,
                    0.00m, 10.00m);

                if (mastery.SeriesAnswerCount >= 10)
                {
                    mastery.PracticePoint = mastery.OfficialPoint;
                    mastery.SeriesAnswerCount = 0;
                }
            }
        }

        // ── RCM-07: Map RecommendedDifficultyLevel ────────────────────────────────
        mastery.RecommendedDifficultyLevel = MapDifficultyLevel(mastery.OfficialPoint);

        // ── RCM-13: Update MasteryStatus ──────────────────────────────────────────
        mastery.NumberDone = Math.Max(0,
            mastery.NumberDone + decimal.ToInt32(tagResult.TotalItems - (existingResult?.TotalItems ?? 0m)));
        mastery.NumCorrect = Math.Max(0,
            mastery.NumCorrect + decimal.ToInt32(tagResult.CorrectItems - (existingResult?.CorrectItems ?? 0m)));
        mastery.AccuracyRate = mastery.NumberDone > 0
            ? Math.Round((decimal)mastery.NumCorrect / mastery.NumberDone * 100m, 2)
            : 0m;
        mastery.MasteryStatus = DetermineMasteryStatus(mastery.NumberDone, mastery.OfficialPoint);
        mastery.LastCalculatedAt = evt.GradedAt;

        // Calculate EarnedPoints and MaxPoints from GradedAnswerDto list
        var answersForTag = (evt.Answers ?? Array.Empty<GradedAnswerDto>())
            .Where(a => a.TagId == tagResult.TagId && !a.IsScoreInvalidated)
            .ToList();

        decimal earnedPoints = answersForTag.Sum(a => a.PointsEarned);
        decimal maxPoints = answersForTag.Sum(a => a.MaxPoints);

        if (answersForTag.Count == 0 || maxPoints == 0)
        {
            earnedPoints = tagResult.EarnedPoints;
            maxPoints = tagResult.MaxPoints;
        }

        // ── RCM-08: Insert StudentTopicSessionResult ────────────────────────────
        if (existingResult is null)
        {
            existingResult = new StudentTopicSessionResult
            {
                StudentTopicSessionResultId = Guid.NewGuid().ToString(),
                StudentId = evt.StudentId,
                SessionId = evt.SessionId,
                TagId = tagResult.TagId,
                CreatedTime = evt.GradedAt
            };
            _db.StudentTopicSessionResults.Add(existingResult);
        }

        existingResult.TotalItems = tagResult.TotalItems;
        existingResult.CorrectItems = tagResult.CorrectItems;
        existingResult.EarnedPoints = earnedPoints;
        existingResult.MaxPoints = maxPoints;
        existingResult.TopicScore = tagResult.TopicScore;
        existingResult.GradeRevision = incomingRevision;

        await _db.SaveChangesAsync(ct);
    }

    // ── RCM-05: Exponential Decay formula ────────────────────────────────────────
    // exam_anchor = Σ(j=1→k) [β^(j-1) × T_j] / Σ(j=1→k) [β^(j-1)]
    // j=1 is the most recent (history[0]); β=0.8.
    private static decimal CalculateExamAnchor(List<ExamHistoryEntry> history)
    {
        if (history.Count == 0) return 5.00m;

        decimal weightedSum = 0m;
        decimal weightSum = 0m;
        decimal weight = 1m; // β^0 = 1 for j=1

        foreach (var entry in history)
        {
            weightedSum += weight * entry.TopicScore;
            weightSum += weight;
            weight *= Beta;
        }

        return Math.Clamp(weightedSum / weightSum, 0.00m, 10.00m);
    }

    // ── RCM-07: Difficulty level mapping ─────────────────────────────────────────
    private static byte MapDifficultyLevel(decimal officialPoint) => officialPoint switch
    {
        < 3.00m => 1,
        < 5.00m => 2,
        < 7.50m => 3,
        _ => 4
    };

    // ── RCM-13: MasteryStatus thresholds ─────────────────────────────────────────
    private static string DetermineMasteryStatus(int numberDone, decimal officialPoint)
    {
        if (numberDone == 0) return "NotLearned";
        if (officialPoint >= MasteredThreshold) return "Mastered";
        return "Learning";
    }

    private static decimal CalculatePracticeDelta(bool isCorrect, GradedAnswerDto answer)
    {
        var difficultyWeight = answer.DifficultyLevel switch
        {
            1 => 0.5m,
            2 => 1.0m,
            3 => 1.5m,
            4 => 2.0m,
            _ => 1.0m
        };
        var timePenalty = answer.TimeSpent < 5 && !answer.IsAbandoned ? 1.5m : 1.0m;
        return isCorrect
            ? 0.05m * difficultyWeight
            : -0.05m * (5.0m - difficultyWeight) * timePenalty;
    }

    private static List<ExamHistoryEntry> DeserializeHistory(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<ExamHistoryEntry>>(json) ?? [];
        }
        catch (JsonException)
        {
            try
            {
                var legacy = JsonSerializer.Deserialize<List<decimal>>(json) ?? [];
                return legacy.Select((score, index) => new ExamHistoryEntry(
                    $"legacy-{index}", 1, score, DateTime.MinValue)).ToList();
            }
            catch (JsonException)
            {
                return [];
            }
        }
    }

    private sealed record ExamHistoryEntry(
        string SessionId,
        int GradeRevision,
        decimal TopicScore,
        DateTime GradedAt);
}
