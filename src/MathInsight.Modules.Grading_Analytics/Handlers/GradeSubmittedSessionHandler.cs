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
        // ── Build per-answer DTOs with primary TagId ──────────────────────────
        var gradedAnswers = new List<GradedAnswerDto>();

        // ── Build per-tag results ────────────────────────────────────────────
        // Track per-tag correctness for PerTagResults
        var tagStats = new Dictionary<Guid, (int Correct, int Total)>();

        foreach (var answer in session.TestAnswers)
        {
            // Find primary topic tag for this question
            var primaryTopic = answer.Question.QuestionTopics
                .FirstOrDefault(qt => qt.IsPrimary);

            var tagId = primaryTopic?.TagId ?? Guid.Empty;

            // Determine abandoned status using same logic as GradingEngine
            bool isAbandoned = IsAbandoned(answer, answer.Question.QuestionType);

            gradedAnswers.Add(new GradedAnswerDto
            {
                QuestionId = answer.QuestionId,
                TagId = tagId,
                IsCorrect = answer.IsCorrect == true,
                PointsEarned = answer.PointsEarned,
                MaxPoints = answer.Question.DefaultPoint,
                TimeSpent = answer.TimeSpent ?? 0,
                DifficultyLevel = answer.Question.DifficultyLevel,
                QuestionNo = answer.QuestionNo,
                IsAbandoned = isAbandoned
            });

            // Accumulate per-tag stats (skip questions without a primary tag)
            if (tagId != Guid.Empty)
            {
                if (!tagStats.TryGetValue(tagId, out var stats))
                    stats = (0, 0);

                stats.Total++;
                if (answer.IsCorrect == true)
                    stats.Correct++;

                tagStats[tagId] = stats;
            }
        }

        // ── Build PerTagResults ──────────────────────────────────────────────
        var perTagResults = tagStats
            .Select(kv => new TopicGradeResult
            {
                TagId = kv.Key,
                TopicScore = kv.Value.Total > 0
                    ? Math.Round((decimal)kv.Value.Correct / kv.Value.Total * 10.0m, 2)
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
