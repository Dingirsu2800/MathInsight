using System.Text.Json;
using MathInsight.Modules.Grading_Analytics.Persistence;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;
using MathInsight.Shared.Events;
using MathInsight.Shared.Questions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MathInsight.Modules.Grading_Analytics.Services;

/// <summary>
/// Core grading orchestration logic shared by MediatR handler (Practice) and
/// MassTransit consumer (Exam).
///
/// Responsibilities:
///   1. Load TestSession with all required navigation properties
///   2. Load TestQuestion scoring snapshots and resolve onto TestAnswer entities
///   3. Validate TestSession.Status == InProgress
///   4. Run GradingEngine.Grade() synchronously
///   5. Write results in a single transaction (DC-05: Transactional Atomicity)
///   6. Set TestSession.Status = Graded; increment GradeRevision; preserve SubmissionType
///   7. Build and return GradeCalculatedEvent (G3)
///
/// Retry policy:
///   U2 — EF Core's EnableRetryOnFailure is configured on the DbContext (3 retries,
///   exponential backoff). For explicit transactions we use CreateExecutionStrategy()
///   so that EF retries the entire unit-of-work on transient failures.
/// </summary>
public class GradingOrchestrator : IGradingOrchestrator
{
    private const decimal PrimaryTagWeight = 0.65m;

    private readonly GradingDbContext _db;
    private readonly IGradingEngine _gradingEngine;
    private readonly ILogger<GradingOrchestrator> _logger;

    public GradingOrchestrator(
        GradingDbContext db,
        IGradingEngine gradingEngine,
        ILogger<GradingOrchestrator> logger)
    {
        _db = db;
        _gradingEngine = gradingEngine;
        _logger = logger;
    }

    public async Task<GradeCalculatedEvent?> GradeSessionAsync(
        string sessionId,
        TestSubmittedEvent notification,
        CancellationToken cancellationToken = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        GradeCalculatedEvent? gradeEvent = null;

        try
        {
            await strategy.ExecuteAsync(async ct =>
            {
                gradeEvent = await GradeSessionInTransactionAsync(sessionId, notification, ct);
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogInformation(
                "Grading completed for session {SessionId} (Score={Score}, " +
                "Correct={NumCorrect}, Incorrect={NumIncorrect}, Abandoned={NumAbandoned}, " +
                "GradeRevision={GradeRevision})",
                gradeEvent.SessionId, gradeEvent.Score,
                gradeEvent.NumCorrect, gradeEvent.NumIncorrect, gradeEvent.NumAbandoned,
                gradeEvent.GradeRevision);
        }

        return gradeEvent;
    }

    private async Task<GradeCalculatedEvent?> GradeSessionInTransactionAsync(
        string sessionId,
        TestSubmittedEvent notification,
        CancellationToken cancellationToken)
    {
        var session = await _db.TestSessions
            .Include(item => item.TestAnswers)
                .ThenInclude(answer => answer.Question)
                    .ThenInclude(question => question.Answers)
            .Include(item => item.TestAnswers)
                .ThenInclude(answer => answer.Question)
                    .ThenInclude(question => question.Parts)
            .Include(item => item.TestAnswers)
                .ThenInclude(answer => answer.Question)
                    .ThenInclude(question => question.QuestionTopics)
            .Include(item => item.TestAnswers)
                .ThenInclude(answer => answer.SelectedOptions)
            .Include(item => item.TestAnswers)
                .ThenInclude(answer => answer.AnswerParts)
                    .ThenInclude(part => part.QuestionPart)
            .FirstOrDefaultAsync(item => item.SessionId == sessionId, cancellationToken);

        if (session is null)
        {
            _logger.LogWarning("Session {SessionId} was not found for grading.", sessionId);
            return null;
        }

        if (!string.Equals(session.Status, "InProgress", StringComparison.OrdinalIgnoreCase))
            return null;
        }

        // ── Load TestQuestion scoring snapshots ───────────────────────────────
        // TestQuestion has composite PK (TestId, QuestionId). We load all TestQuestions
        // for this Test and resolve them onto TestAnswer.TestQuestion manually,
        // because EF cannot auto-navigate this cross-entity relationship.
        var testQuestions = await _db.TestQuestions
            .AsNoTracking()
            .Where(tq => tq.TestId == session.TestId)
            .ToDictionaryAsync(tq => tq.QuestionId, ct);

        foreach (var answer in session.TestAnswers)
        {
            if (testQuestions.TryGetValue(answer.QuestionId, out var tq))
            {
                answer.TestQuestion = tq;
            }
        }

        // ── Load Test for MaxScore ────────────────────────────────────────────
        var test = await _db.Tests
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TestId == session.TestId, ct);

        // ── Run grading engine synchronously ──────────────────────────────────
        var gradingResult = _gradingEngine.Grade(session);

        var testQuestions = await _db.TestQuestions
            .AsNoTracking()
            .Include(item => item.QuestionVersion)
            .Where(item => item.TestId == session.TestId)
            .ToDictionaryAsync(
                item => item.QuestionId,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);

        foreach (var answer in session.TestAnswers)
        {
            // Update TestSession with grading results
            session.Status = "Graded";
            session.Score = gradingResult.Score;
            session.NumCorrect = gradingResult.NumCorrect;
            session.NumIncorrect = gradingResult.NumIncorrect;
            session.NumAbandoned = gradingResult.NumAbandoned;
            session.GradeRevision++;
            // SubmissionType is preserved — it was set by Testing during submit

            if (testQuestion.QuestionVersion.SnapshotSchemaVersion != 2)
                throw new InvalidOperationException(
                    $"Unsupported snapshot schema for version '{testQuestion.QuestionVersionId}'.");

            answer.Snapshot = JsonSerializer.Deserialize<QuestionSnapshotV2>(
                testQuestion.QuestionVersion.AnswersSnapshot)
                ?? throw new InvalidOperationException(
                    $"Invalid snapshot JSON for version '{testQuestion.QuestionVersionId}'.");
            answer.MaxPointsSnapshot = testQuestion.MaxPointsSnapshot;
            answer.ScoringRuleSnapshot = testQuestion.ScoringRuleSnapshot;
            answer.IsScoreInvalidated = testQuestion.IsScoreInvalidated;
        }

        var gradingResult = _gradingEngine.Grade(session);
        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        session.Status = "Graded";
        session.Score = gradingResult.Score;
        session.NumCorrect = gradingResult.NumCorrect;
        session.NumIncorrect = gradingResult.NumIncorrect;
        session.NumAbandoned = gradingResult.NumAbandoned;
        session.GradeRevision = Math.Max(1, session.GradeRevision + 1);
        session.SubmissionType = notification.SubmissionType;
        session.EndTime = notification.SubmittedTime;
        session.Duration = Math.Max(
            0,
            (int)Math.Round((notification.SubmittedTime -
                (session.StartTime ?? notification.SubmittedTime)).TotalSeconds));

        // ── G3: Build GradeCalculatedEvent ────────────────────────────────────
        return BuildGradeCalculatedEvent(session, gradingResult, notification, testQuestions, test);
    }

    /// <summary>
    /// Default weight for the primary (Tag Chính) topic when a question has multiple tags.
    /// BR-13/14/15: w_main ∈ [0.60, 0.70], default 0.65.
    /// </summary>
    private const decimal DefaultPrimaryWeight = 0.65m;

    /// <summary>
    /// Builds the GradeCalculatedEvent contract (G3) from grading results.
    /// Uses MathInsight.Shared.Events.GradeCalculatedEvent — no local copy.
    ///
    /// Phase 6 (Unified Multi-Tag v4.1):
    ///   - Populates TagWeights per answer from QuestionTopics (BR-13/14/15)
    ///   - Populates NormalizedScore per answer: PointsEarned / MaxPoints × 10.0
    ///   - Populates MaxPoints per answer: TestQuestion.MaxPointsSnapshot (or Question.DefaultWeight fallback)
    ///   - Expands PerTagResults to ALL tags (primary + secondary) with weighted Tầng 1–2 formula
    /// </summary>
    private static GradeCalculatedEvent BuildGradeCalculatedEvent(
        TestSession session,
        GradingResult gradingResult,
        TestSubmittedEvent notification,
        Dictionary<Guid, TestQuestion> testQuestions,
        Test? test)
    {
        var gradedAnswers = new List<GradedAnswerDto>();

        // ── Track per-tag weighted contributions for Tầng 1–2 formula ────────
        // Key: TagId → list of c_{q,i} contributions (one per question containing this tag)
        var tagContributions = new Dictionary<Guid, List<decimal>>();
        // Track per-tag correct/total counts for PerTagResults
        var tagStats = new Dictionary<Guid, (int Correct, int Total)>();

        foreach (var answer in session.TestAnswers)
        {
            var questionTopics = answer.Question.QuestionTopics;
            var primaryTopic = questionTopics.FirstOrDefault(qt => qt.IsPrimary);
            var primaryTagId = primaryTopic?.TagId ?? Guid.Empty;

            // ── Phase 6: Build TagWeights from QuestionTopics (BR-13/14/15) ──
            var tagWeights = BuildTagWeights(questionTopics);

            // ── MaxPoints from TestQuestion.MaxPointsSnapshot ────────────────
            decimal maxPoints = testQuestions.TryGetValue(answer.QuestionId, out var tq)
                ? tq.MaxPointsSnapshot
                : answer.Question.DefaultWeight;

            // ── Phase 6: NormalizedScore = PointsEarned / MaxPoints × 10.0 ──
            decimal normalizedScore = maxPoints > 0
                ? Math.Round(answer.PointsEarned / maxPoints * 10.0m, 2)
                : 0m;

            // Determine abandoned status using same logic as GradingEngine
            bool isAbandoned = IsAbandoned(answer, answer.Question.QuestionType);

            // Determine invalidation status
            bool isInvalidated = tq?.IsScoreInvalidated ?? false;

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
                DifficultyLevel = 1,
                QuestionNo = answer.QuestionNo,
                IsAbandoned = isAbandoned,
                IsScoreInvalidated = isInvalidated
            });

            // ── Phase 6: Accumulate per-tag stats for ALL tags ───────────────
            // Skip invalidated questions from tag statistics
            if (!isInvalidated)
            {
                foreach (var tw in tagWeights)
                {
                    if (tw.TagId == Guid.Empty) continue;

                    // Tầng 1: c_{q,i} = s_q × w_{iq}
                    decimal contribution = normalizedScore * tw.Weight;

                    if (!tagContributions.TryGetValue(tw.TagId, out var contributions))
                    {
                        contributions = new List<decimal>();
                        tagContributions[tw.TagId] = contributions;
                    }
                    contributions.Add(contribution);

                    // Track correct/total per tag
                    if (!tagStats.TryGetValue(tw.TagId, out var stats))
                        stats = (0, 0);
                    stats.Total++;
                    if (answer.IsCorrect == true)
                        stats.Correct++;
                    tagStats[tw.TagId] = stats;
                }
            }
        }

        // ── Phase 6: Build PerTagResults with Tầng 2 weighted TopicScore ────
        var perTagResults = tagContributions
            .Select(kv =>
            {
                // Tầng 2: T_j^{(i)} = avg(c_{q,i}) across all questions containing tag i
                decimal topicScore = kv.Value.Count > 0
                    ? Math.Round(kv.Value.Average(), 2)
                    : 0m;

                var (correct, total) = tagStats.TryGetValue(kv.Key, out var s) ? s : (0, 0);

                return new TopicGradeResult
                {
                    TagId = kv.Key,
                    TopicScore = Math.Clamp(topicScore, 0.00m, 10.00m),
                    CorrectCount = correct,
                    TotalCount = total
                };
            })
            .ToList();

        return new GradeCalculatedEvent
        {
            SessionId = session.SessionId,
            StudentId = session.StudentId,
            TestId = session.TestId,
            GradeRevision = session.GradeRevision,
            TestFormat = session.TestFormat,
            Score = gradingResult.Score,
            NumCorrect = gradingResult.NumCorrect,
            NumIncorrect = gradingResult.NumIncorrect,
            NumAbandoned = gradingResult.NumAbandoned,
            GradeRevision = session.GradeRevision,
            PerTagResults = perTagResults,
            Answers = gradedAnswers,
            GradedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Builds TagWeightEntry list from QuestionTopics for a single question.
    /// BR-13/14/15 weight rules:
    ///   - Single tag: w = 1.0
    ///   - Tag Chính (primary): w_main = 0.65
    ///   - Tag Phụ (secondary): w_sub_i = (1 − w_main) / N_sub
    /// Sum of all weights = 1.0.
    /// </summary>
    private static List<TagWeightEntry> BuildTagWeights(ICollection<QuestionTopic> questionTopics)
    {
        if (questionTopics.Count == 0)
            return [];

        if (questionTopics.Count == 1)
        {
            var qt = questionTopics.First();
            return [new TagWeightEntry
            {
                TagId = qt.TagId,
                Weight = 1.0m,
                IsPrimary = qt.IsPrimary
            }];
        }

        // Multi-tag question: primary gets w_main, each secondary gets (1 - w_main) / N_sub
        var primary = questionTopics.FirstOrDefault(qt => qt.IsPrimary);
        var secondaries = questionTopics.Where(qt => !qt.IsPrimary).ToList();

        decimal wMain = DefaultPrimaryWeight;
        decimal wSub = secondaries.Count > 0
            ? (1.0m - wMain) / secondaries.Count
            : 0m;

        var weights = new List<TagWeightEntry>();

        if (primary is not null)
        {
            weights.Add(new TagWeightEntry
            {
                TagId = primary.TagId,
                Weight = wMain,
                IsPrimary = true
            });
        }

        foreach (var sec in secondaries)
        {
            weights.Add(new TagWeightEntry
            {
                TagId = sec.TagId,
                Weight = wSub,
                IsPrimary = false
            });
        }

        return weights;
    }

    /// <summary>
    /// Mirrors GradingEngine.IsAbandoned logic for populating GradedAnswerDto.IsAbandoned.
    /// </summary>
    private static bool IsAbandoned(TestAnswer answer, string questionType)
    {
        var topics = snapshot?.Topics
            .Select(item => (item.TagId, item.IsPrimary))
            .ToList()
            ?? question.QuestionTopics
                .Select(item => (item.TagId, item.IsPrimary))
                .ToList();

        if (topics.Count == 0)
            return [];
        if (topics.Count == 1)
            return [new TagWeightEntry
            {
                TagId = topics[0].TagId,
                Weight = 1m,
                IsPrimary = true
            }];

        var primaryIndex = topics.FindIndex(item => item.IsPrimary);
        if (primaryIndex < 0)
            primaryIndex = 0;
        var secondaryWeight = (1m - PrimaryTagWeight) / (topics.Count - 1);

        return topics.Select((topic, index) => new TagWeightEntry
        {
            TagId = topic.TagId,
            Weight = index == primaryIndex ? PrimaryTagWeight : secondaryWeight,
            IsPrimary = index == primaryIndex
        }).ToList();
    }

    private struct TagStat
    {
        public decimal TotalItems;
        public decimal CorrectItems;
        public decimal EarnedPoints;
        public decimal MaxPoints;
    }
}
