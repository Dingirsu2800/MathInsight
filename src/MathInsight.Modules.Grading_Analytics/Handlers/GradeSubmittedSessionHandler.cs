using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MathInsight.Shared.Events;
using MathInsight.Modules.Grading_Analytics.Persistence;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;
using MathInsight.Modules.Grading_Analytics.Services;

namespace MathInsight.Modules.Grading_Analytics.Handlers;

/// <summary>
/// Synchronous MVP grading handler — called in-process by the Testing submit flow
/// via TestSubmittedEvent (MediatR notification).
///
/// Responsibilities:
///   1. Validate TestSession.Status == InProgress
///   2. Run GradingEngine.Grade() synchronously
///   3. Write results in a single transaction (DC-05: Transactional Atomicity)
///   4. Set TestSession.Status = Graded; preserve SubmissionType from Testing
///   5. Publish GradeCalculatedEvent after commit (G3)
///
/// Retry policy:
///   U2 — EF Core's EnableRetryOnFailure is configured on the DbContext (3 retries,
///   exponential backoff). For explicit transactions we use CreateExecutionStrategy()
///   so that EF retries the entire unit-of-work on transient failures.
///
/// SLA:
///   Practice < 2.0s, Exam target < 5.0s.
/// </summary>
public class GradeSubmittedSessionHandler : INotificationHandler<TestSubmittedEvent>
{
    private readonly GradingDbContext _db;
    private readonly IGradingEngine _gradingEngine;
    private readonly IPublisher _publisher;
    private readonly ILogger<GradeSubmittedSessionHandler> _logger;

    public GradeSubmittedSessionHandler(
        GradingDbContext db,
        IGradingEngine gradingEngine,
        IPublisher publisher,
        ILogger<GradeSubmittedSessionHandler> logger)
    {
        _db = db;
        _gradingEngine = gradingEngine;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Handle(TestSubmittedEvent notification, CancellationToken cancellationToken)
    {
        var sessionId = notification.SessionId;

        _logger.LogInformation(
            "Grading session {SessionId} started (TestFormat={TestFormat})",
            sessionId, notification.TestFormat);

        // ── U2: EF Core execution strategy wraps the explicit transaction ─────────
        // EnableRetryOnFailure is configured on the DbContext.
        // When using explicit transactions, we MUST use CreateExecutionStrategy()
        // so EF can retry the entire unit-of-work (including BeginTransaction)
        // on transient failures — 3 retries with exponential backoff.
        var strategy = _db.Database.CreateExecutionStrategy();

        GradeCalculatedEvent? gradeEvent = null;

        try
        {
            await strategy.ExecuteAsync(async ct =>
            {
                gradeEvent = await GradeSessionInTransactionAsync(sessionId, notification, ct);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            // All retries exhausted — transaction rolled back.
            // Session stays InProgress so the student can retry submit.
            _logger.LogError(ex,
                "Grading failed for session {SessionId} after all retries. " +
                "Session remains InProgress. Error: {Error}",
                sessionId, ex.Message);
            return;
        }

        // ── G3: Publish GradeCalculatedEvent AFTER commit ─────────────────────────
        if (gradeEvent is not null)
        {
            await _publisher.Publish(gradeEvent, cancellationToken);

            _logger.LogInformation(
                "GradeCalculatedEvent published for session {SessionId} (Score={Score}, " +
                "Correct={NumCorrect}, Incorrect={NumIncorrect}, Abandoned={NumAbandoned})",
                gradeEvent.SessionId, gradeEvent.Score,
                gradeEvent.NumCorrect, gradeEvent.NumIncorrect, gradeEvent.NumAbandoned);
        }
    }

    /// <summary>
    /// Loads session, runs grading engine, writes results in a single transaction (DC-05).
    /// Returns the GradeCalculatedEvent to publish after commit.
    /// </summary>
    private async Task<GradeCalculatedEvent?> GradeSessionInTransactionAsync(
        Guid sessionId,
        TestSubmittedEvent notification,
        CancellationToken ct)
    {
        // ── Load session with all required navigation properties ───────────────
        var session = await _db.TestSessions
            .Include(s => s.TestAnswers)
                .ThenInclude(a => a.Question)
                    .ThenInclude(q => q.Answers)
            .Include(s => s.TestAnswers)
                .ThenInclude(a => a.Question)
                    .ThenInclude(q => q.Parts)
            .Include(s => s.TestAnswers)
                .ThenInclude(a => a.Question)
                    .ThenInclude(q => q.QuestionTopics)
            .Include(s => s.TestAnswers)
                .ThenInclude(a => a.SelectedOptions)
            .Include(s => s.TestAnswers)
                .ThenInclude(a => a.AnswerParts)
                    .ThenInclude(ap => ap.QuestionPart)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);

        if (session is null)
        {
            _logger.LogWarning("Session {SessionId} not found for grading", sessionId);
            return null;
        }

        // ── Validate status ───────────────────────────────────────────────────
        if (!string.Equals(session.Status, "InProgress", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Session {SessionId} status is '{Status}', expected 'InProgress'. Skipping grading.",
                sessionId, session.Status);
            return null;
        }

        // ── Run grading engine synchronously ──────────────────────────────────
        var gradingResult = _gradingEngine.Grade(session);

        // ── DC-05: Wrap writes in single transaction ──────────────────────────
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            // Update TestSession with grading results
            session.Status = "Graded";
            session.Score = gradingResult.Score;
            session.NumCorrect = gradingResult.NumCorrect;
            session.NumIncorrect = gradingResult.NumIncorrect;
            session.NumAbandoned = gradingResult.NumAbandoned;
            // SubmissionType is preserved — it was set by Testing during submit

            // TestAnswer entities (IsCorrect, PointsEarned) are already mutated
            // in-place by GradingEngine.Grade(). EF change tracker will persist them.

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            // ── DC-05: On failure, rollback → session stays InProgress ─────
            _logger.LogError(ex,
                "Transaction failed for grading session {SessionId}. Rolling back. Error: {Error}",
                sessionId, ex.Message);

            await transaction.RollbackAsync(ct);
            throw; // Re-throw so the EF execution strategy can retry
        }

        // ── G3: Build GradeCalculatedEvent ────────────────────────────────────
        return BuildGradeCalculatedEvent(session, gradingResult, notification);
    }

    /// <summary>
    /// Builds the GradeCalculatedEvent contract (G3) from grading results.
    /// Uses MathInsight.Shared.Events.GradeCalculatedEvent — no local copy.
    /// </summary>
    private static GradeCalculatedEvent BuildGradeCalculatedEvent(
        TestSession session,
        GradingResult gradingResult,
        TestSubmittedEvent notification)
    {
        // ── Default w_main for primary tag (BR-13) ────────────────────────────
        const decimal DefaultWMain = 0.65m;

        // ── Build per-answer DTOs with multi-tag weights (v4.1) ───────────────
        var gradedAnswers = new List<GradedAnswerDto>();

        // ── Track per-tag weighted contributions for Tầng 1–2 TopicScore ──────
        // Key: TagId, Value: list of c_{q,i} contributions + correctness stats
        var tagContributions = new Dictionary<Guid, (List<decimal> Contributions, int Correct, int Total)>();

        foreach (var answer in session.TestAnswers)
        {
            // Load ALL QuestionTopics (primary + secondary)
            var allTopics = answer.Question.QuestionTopics.ToList();

            // Find primary topic tag
            var primaryTopic = allTopics.FirstOrDefault(qt => qt.IsPrimary);
            var primaryTagId = primaryTopic?.TagId ?? Guid.Empty;

            // ── Calculate tag weights per BR-13/14/15 ─────────────────────────
            var tagWeights = CalculateTagWeights(allTopics, DefaultWMain);

            // ── Calculate NormalizedScore s_q ──────────────────────────────────
            decimal maxPoints = answer.Question.DefaultPoint;
            decimal normalizedScore = maxPoints > 0
                ? Math.Round(answer.PointsEarned / maxPoints * 10.0m, 2)
                : 0m;

            // Determine abandoned status using same logic as GradingEngine
            bool isAbandoned = IsAbandoned(answer, answer.Question.QuestionType);

            gradedAnswers.Add(new GradedAnswerDto
            {
                QuestionId = answer.QuestionId,
                TagId = primaryTagId,
                TagWeights = tagWeights,
                NormalizedScore = normalizedScore,
                IsCorrect = answer.IsCorrect == true,
                PointsEarned = answer.PointsEarned,
                MaxPoints = maxPoints,
                TimeSpent = answer.TimeSpent ?? 0,
                DifficultyLevel = answer.Question.DifficultyLevel,
                QuestionNo = answer.QuestionNo,
                IsAbandoned = isAbandoned
            });

            // ── Accumulate per-tag stats for ALL tags (Tầng 1–2) ──────────────
            foreach (var tw in tagWeights)
            {
                if (tw.TagId == Guid.Empty) continue;

                if (!tagContributions.TryGetValue(tw.TagId, out var stats))
                    stats = (new List<decimal>(), 0, 0);

                // Tầng 1: c_{q,i} = s_q × w_{iq}
                stats.Contributions.Add(normalizedScore * tw.Weight);
                stats.Total++;
                if (answer.IsCorrect == true)
                    stats.Correct++;

                tagContributions[tw.TagId] = stats;
            }
        }

        // ── Build PerTagResults using Tầng 2: T_j^{(i)} = avg(c_{q,i}) ───────
        var perTagResults = tagContributions
            .Select(kv => new TopicGradeResult
            {
                TagId = kv.Key,
                TopicScore = kv.Value.Contributions.Count > 0
                    ? Math.Clamp(Math.Round(kv.Value.Contributions.Average(), 2), 0.00m, 10.00m)
                    : 0m,
                CorrectCount = kv.Value.Correct,
                TotalCount = kv.Value.Total
            })
            .ToList();

        return new GradeCalculatedEvent
        {
            SessionId = session.SessionId,
            StudentId = session.StudentId,
            TestId = session.TestId,
            TestFormat = session.TestFormat,
            Score = gradingResult.Score,
            NumCorrect = gradingResult.NumCorrect,
            NumIncorrect = gradingResult.NumIncorrect,
            NumAbandoned = gradingResult.NumAbandoned,
            PerTagResults = perTagResults,
            Answers = gradedAnswers,
            GradedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Calculates tag weights for a question's topics per BR-13/14/15.
    /// Single-tag: w = 1.0.
    /// Multi-tag: primary gets w_main (default 0.65), each secondary gets (1 - w_main) / N_sub.
    /// Sum of all weights = 1.0.
    /// </summary>
    private static List<TagWeightEntry> CalculateTagWeights(
        List<QuestionTopic> allTopics,
        decimal wMain)
    {
        if (allTopics.Count == 0)
            return [];

        // Single-tag question: degenerate case, w = 1.0
        if (allTopics.Count == 1)
        {
            return [new TagWeightEntry
            {
                TagId = allTopics[0].TagId,
                Weight = 1.0m,
                IsPrimary = allTopics[0].IsPrimary
            }];
        }

        // Multi-tag: apply BR-13/14/15
        var primary = allTopics.FirstOrDefault(t => t.IsPrimary);
        var secondaries = allTopics.Where(t => !t.IsPrimary).ToList();
        int nSub = secondaries.Count;
        decimal wSub = nSub > 0 ? (1.0m - wMain) / nSub : 0m;

        var result = new List<TagWeightEntry>();

        if (primary != null)
        {
            result.Add(new TagWeightEntry
            {
                TagId = primary.TagId,
                Weight = wMain,
                IsPrimary = true
            });
        }

        foreach (var sec in secondaries)
        {
            result.Add(new TagWeightEntry
            {
                TagId = sec.TagId,
                Weight = wSub,
                IsPrimary = false
            });
        }

        return result;
    }

    /// <summary>
    /// Mirrors GradingEngine.IsAbandoned logic for populating GradedAnswerDto.IsAbandoned.
    /// </summary>
    private static bool IsAbandoned(TestAnswer answer, string questionType)
    {
        var typeNormalized = questionType.Replace("_", "").Replace(" ", "").ToUpperInvariant();
        return typeNormalized switch
        {
            "SINGLECHOICE" => answer.AnswerId is null,
            "TRUEFALSE" => answer.AnswerId is null,
            "MULTIPLESELECT" => answer.SelectedOptions.Count == 0,
            "MULTIPLECHOICE" => answer.SelectedOptions.Count == 0,
            "SHORTANSWER" => string.IsNullOrWhiteSpace(answer.ShortAnswerText),
            "COMPOSITE" => answer.AnswerParts.Count == 0 || answer.AnswerParts.All(p =>
                p.BooleanAnswer == null && 
                string.IsNullOrWhiteSpace(p.TextAnswer) && 
                p.NumericAnswer == null),
            _ => true
        };
    }
}
