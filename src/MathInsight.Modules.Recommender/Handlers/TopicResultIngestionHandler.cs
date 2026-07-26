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
/// </summary>
public sealed class TopicResultIngestionHandler : INotificationHandler<GradeCalculatedEvent>
{
    private const decimal Beta = 0.8m;
    private const int MaxExamHistory = 5;
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

        var student = await _db.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.StudentId == notification.StudentId, cancellationToken);

        foreach (var tagResult in notification.PerTagResults)
        {
            await IngestTopicResultAsync(notification, tagResult, cancellationToken);
        }

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
                OfficialPoint = 5.00m,
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
            var hasUsableEvidence = tagResult.TotalItems > 0m;

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
                history.RemoveAt(history.Count - 1);

            mastery.ExamAnchor = CalculateExamAnchor(history);
            mastery.ExamHistory = JsonSerializer.Serialize(history);

            mastery.OfficialPoint = Math.Clamp(
                0.7m * mastery.ExamAnchor + 0.3m * mastery.PracticePoint,
                0.00m, 10.00m);

            mastery.PracticePoint = mastery.OfficialPoint;
        }
        else if (string.Equals(evt.TestFormat, "Practice", StringComparison.OrdinalIgnoreCase))
        {
            mastery.LastPracticedTime = evt.GradedAt;

            var tagAnswers = (evt.Answers ?? [])
                .Where(answer => HasTag(answer, tagResult.TagId))
                .OrderBy(a => a.QuestionNo)
                .ToList();

            foreach (var ans in tagAnswers)
            {
                if (existingResult is not null && ans.IsScoreInvalidated && ans.MachineIsCorrect is bool machineIsCorrect)
                {
                    decimal previousDelta = CalculatePracticeDelta(machineIsCorrect, ans);
                    decimal tagWeight = GetTagWeight(ans, tagResult.TagId);

                    mastery.PracticePoint = Math.Clamp(
                        mastery.PracticePoint - (previousDelta * tagWeight),
                        0.00m,
                        10.00m);

                    mastery.SeriesAnswerCount = Math.Max(0, mastery.SeriesAnswerCount - 1);
                }
                else if (!ans.IsScoreInvalidated)
                {
                    decimal delta = CalculatePracticeDelta(ans.IsCorrect, ans);
                    decimal tagWeight = GetTagWeight(ans, tagResult.TagId);

                    mastery.PracticePoint = Math.Clamp(
                        mastery.PracticePoint + (delta * tagWeight),
                        0.00m,
                        10.00m);

                    mastery.SeriesAnswerCount++;
                }
            }

            mastery.OfficialPoint = Math.Clamp(
                0.7m * mastery.ExamAnchor + 0.3m * mastery.PracticePoint,
                0.00m, 10.00m);

            if (mastery.SeriesAnswerCount >= 10)
            {
                mastery.PracticePoint = mastery.OfficialPoint;
                mastery.SeriesAnswerCount -= 10;
            }
        }

        // ── RCM-07: Map RecommendedDifficultyLevel ────────────────────────────────
        mastery.RecommendedDifficultyLevel = MapDifficultyLevel(mastery.OfficialPoint);

        // ── RCM-13: Update MasteryStatus ──────────────────────────────────────────
        int numDoneDelta = decimal.ToInt32(tagResult.TotalItems - (existingResult?.TotalItems ?? 0m));
        int numCorrectDelta = decimal.ToInt32(tagResult.CorrectItems - (existingResult?.CorrectItems ?? 0m));

        mastery.NumberDone = Math.Max(0, mastery.NumberDone + numDoneDelta);
        mastery.NumCorrect = Math.Max(0, mastery.NumCorrect + numCorrectDelta);
        mastery.AccuracyRate = mastery.NumberDone > 0
            ? Math.Round((decimal)mastery.NumCorrect / mastery.NumberDone * 100m, 2)
            : 0m;
        mastery.MasteryStatus = DetermineMasteryStatus(mastery.NumberDone, mastery.OfficialPoint);
        mastery.LastCalculatedAt = evt.GradedAt;

        var answersForTag = (evt.Answers ?? [])
            .Where(answer => HasTag(answer, tagResult.TagId) && !answer.IsScoreInvalidated)
            .ToList();

        decimal earnedPoints = answersForTag.Sum(a => a.PointsEarned);
        decimal maxPoints = answersForTag.Sum(a => a.MaxPoints);

        // ── RCM-08: Insert or Update StudentTopicSessionResult ───────────────────
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

    private static decimal CalculateExamAnchor(List<ExamHistoryEntry> history)
    {
        if (history.Count == 0) return 5.00m;

        decimal weightedSum = 0m;
        decimal weightSum = 0m;
        decimal weight = 1m;

        foreach (var entry in history)
        {
            weightedSum += weight * entry.TopicScore;
            weightSum += weight;
            weight *= Beta;
        }

        return Math.Clamp(weightedSum / weightSum, 0.00m, 10.00m);
    }

    private static byte MapDifficultyLevel(decimal officialPoint) => officialPoint switch
    {
        < 3.00m => 1,
        < 5.00m => 2,
        < 7.50m => 3,
        _ => 4
    };

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

    private static bool HasTag(GradedAnswerDto answer, string tagId)
        => answer.TagWeights.Any(weight => string.Equals(
               weight.TagId,
               tagId,
               StringComparison.OrdinalIgnoreCase)) ||
           (answer.TagWeights.Count == 0 && string.Equals(
               answer.TagId,
               tagId,
               StringComparison.OrdinalIgnoreCase));

    private static decimal GetTagWeight(GradedAnswerDto answer, string tagId)
        => answer.TagWeights.FirstOrDefault(weight => string.Equals(
               weight.TagId,
               tagId,
               StringComparison.OrdinalIgnoreCase))?.Weight ?? 1m;

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
