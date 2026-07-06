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

        // We need a student grade to update CompetencyPoint.
        // For MVP, derive grade from context if available; otherwise default to 0 (unknown).
        // Full grade resolution from Identity module is deferred to a later phase.
        const int defaultGrade = 0;

        foreach (var tagResult in notification.PerTagResults)
        {
            await IngestTopicResultAsync(notification, tagResult, cancellationToken);
        }

        // RCM-12 / G1: Recalculate CompetencyPoint once after all tags are updated.
        await _competencyEngine.RecalculateAsync(notification.StudentId, defaultGrade, cancellationToken);
    }

    private async Task IngestTopicResultAsync(
        GradeCalculatedEvent evt,
        TopicGradeResult tagResult,
        CancellationToken ct)
    {
        // ── RCM-08: Idempotency ──────────────────────────────────────────────────
        // Skip if (session_id, tag_id) already exists — safe to call multiple times.
        bool alreadyIngested = await _db.StudentTopicSessionResults
            .AnyAsync(r => r.SessionId == evt.SessionId && r.TagId == tagResult.TagId, ct);

        if (alreadyIngested)
            return;

        // ── U3 / RCM no-history: Lazy-create TagsMastery if absent ───────────────
        var mastery = await _db.TagsMasteries
            .FirstOrDefaultAsync(tm => tm.StudentId == evt.StudentId && tm.TagId == tagResult.TagId, ct);

        if (mastery is null)
        {
            mastery = new TagsMastery
            {
                TagsMasteryId = Guid.NewGuid(),
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

        decimal pointBefore = mastery.OfficialPoint;

        // ── RCM-05: Update ExamAnchor using Exponential Decay ───────────────────
        var history = DeserializeHistory(mastery.ExamHistory);
        history.Insert(0, tagResult.TopicScore); // prepend — newest first
        if (history.Count > MaxExamHistory)
            history.RemoveAt(history.Count - 1); // drop oldest

        mastery.ExamAnchor = CalculateExamAnchor(history);
        mastery.ExamHistory = JsonSerializer.Serialize(history);

        // ── RCM-04: Recalculate OfficialPoint ────────────────────────────────────
        mastery.OfficialPoint = Math.Clamp(
            0.7m * mastery.ExamAnchor + 0.3m * mastery.PracticePoint,
            0.00m, 10.00m);

        // ── RCM-07: Map RecommendedDifficultyLevel ────────────────────────────────
        mastery.RecommendedDifficultyLevel = MapDifficultyLevel(mastery.OfficialPoint);

        // ── RCM-13: Update MasteryStatus ──────────────────────────────────────────
        mastery.NumberDone += tagResult.TotalCount;
        mastery.NumCorrect += tagResult.CorrectCount;
        mastery.AccuracyRate = mastery.NumberDone > 0
            ? Math.Round((decimal)mastery.NumCorrect / mastery.NumberDone, 4)
            : 0m;
        mastery.MasteryStatus = DetermineMasteryStatus(mastery.NumberDone, mastery.OfficialPoint);
        mastery.LastCalculatedAt = evt.GradedAt;

        decimal pointAfter = mastery.OfficialPoint;

        // ── RCM-08: Insert StudentTopicSessionResult ────────────────────────────
        _db.StudentTopicSessionResults.Add(new StudentTopicSessionResult
        {
            StudentTopicSessionResultId = Guid.NewGuid(),
            StudentId = evt.StudentId,
            SessionId = evt.SessionId,
            TagId = tagResult.TagId,
            TotalQuestions = tagResult.TotalCount,
            CorrectCount = tagResult.CorrectCount,
            WrongCount = tagResult.TotalCount - tagResult.CorrectCount,
            TopicScore = tagResult.TopicScore,
            PointBefore = pointBefore,
            PointAfter = pointAfter,
            CreatedTime = evt.GradedAt
        });

        await _db.SaveChangesAsync(ct);
    }

    // ── RCM-05: Exponential Decay formula ────────────────────────────────────────
    // exam_anchor = Σ(j=1→k) [β^(j-1) × T_j] / Σ(j=1→k) [β^(j-1)]
    // j=1 is the most recent (history[0]); β=0.8.
    private static decimal CalculateExamAnchor(List<decimal> history)
    {
        if (history.Count == 0) return 5.00m;

        decimal weightedSum = 0m;
        decimal weightSum = 0m;
        decimal weight = 1m; // β^0 = 1 for j=1

        foreach (var score in history)
        {
            weightedSum += weight * score;
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
        _       => 4
    };

    // ── RCM-13: MasteryStatus thresholds ─────────────────────────────────────────
    private static string DetermineMasteryStatus(int numberDone, decimal officialPoint)
    {
        if (numberDone == 0) return "NotLearned";
        if (officialPoint >= MasteredThreshold) return "Mastered";
        return "Learning";
    }

    private static List<decimal> DeserializeHistory(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<decimal>>(json) ?? []; }
        catch { return []; }
    }
}
