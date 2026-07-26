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
            _logger.LogError(exception, "Grading failed for session {SessionId}", sessionId);
            return null;
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

        if (!string.Equals(session.Status, "InProgress", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(session.Status, "Submitted", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Session {SessionId} is not InProgress or Submitted (Status={Status}). Skipping grading.", sessionId, session.Status);
            return null;
        }

        // ── Load TestQuestion scoring snapshots ───────────────────────────────
        var testQuestions = await _db.TestQuestions
            .AsNoTracking()
            .Where(tq => tq.TestId == session.TestId)
            .ToDictionaryAsync(tq => tq.QuestionId, cancellationToken);

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
            .FirstOrDefaultAsync(t => t.TestId == session.TestId, cancellationToken);

        // ── Load TagDifficulty lookup map ─────────────────────────────────────
        var difficultyLevels = await _db.TagDifficulties
            .AsNoTracking()
            .ToDictionaryAsync(td => td.DifficultyId, td => td.LevelValue, StringComparer.OrdinalIgnoreCase, cancellationToken);

        // ── Run grading engine synchronously ──────────────────────────────────
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

        await _db.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        // ── Build and return GradeCalculatedEvent ─────────────────────────────
        return BuildGradeCalculatedEvent(session, gradingResult, notification, testQuestions, test, difficultyLevels);
    }

    private static GradeCalculatedEvent BuildGradeCalculatedEvent(
        TestSession session,
        GradingResult gradingResult,
        TestSubmittedEvent notification,
        Dictionary<string, TestQuestion> testQuestions,
        Test? test,
        Dictionary<string, byte> difficultyLevels)
    {
        var gradedAnswers = new List<GradedAnswerDto>();

        var tagContributions = new Dictionary<string, List<decimal>>();
        var tagStats = new Dictionary<string, (int Correct, int Total)>();

        foreach (var answer in session.TestAnswers)
        {
            var questionTopics = answer.Question.QuestionTopics;
            var primaryTopic = questionTopics.FirstOrDefault(qt => qt.IsPrimary);
            var primaryTagId = primaryTopic?.TagId ?? string.Empty;

            var tagWeights = BuildTagWeights(questionTopics);

            decimal maxPoints = testQuestions.TryGetValue(answer.QuestionId, out var tq)
                ? tq.MaxPointsSnapshot
                : answer.Question.DefaultWeight;

            decimal normalizedScore = maxPoints > 0
                ? Math.Round(answer.PointsEarned / maxPoints * 10.0m, 2)
                : 0m;

            bool isAbandoned = IsAbandoned(answer, answer.Question.QuestionType);
            bool isInvalidated = tq?.IsScoreInvalidated ?? false;

            byte difficultyLevel = 1;
            if (!string.IsNullOrEmpty(answer.Question.DifficultyId) &&
                difficultyLevels.TryGetValue(answer.Question.DifficultyId, out var level))
            {
                difficultyLevel = level;
            }

            gradedAnswers.Add(new GradedAnswerDto
            {
                QuestionId = answer.QuestionId,
                TagId = primaryTagId,
                TagWeights = tagWeights,
                NormalizedScore = normalizedScore,
                IsCorrect = answer.IsCorrect == true,
                MachineIsCorrect = answer.IsCorrect,
                PointsEarned = answer.PointsEarned,
                MaxPoints = maxPoints,
                TimeSpent = answer.TimeSpent ?? 0,
                DifficultyLevel = difficultyLevel,
                QuestionNo = answer.QuestionNo,
                IsAbandoned = isAbandoned,
                IsScoreInvalidated = isInvalidated
            });

            if (!isInvalidated)
            {
                foreach (var tw in tagWeights)
                {
                    if (string.IsNullOrWhiteSpace(tw.TagId)) continue;

                    decimal contribution = normalizedScore * tw.Weight;

                    if (!tagContributions.TryGetValue(tw.TagId, out var contributions))
                    {
                        contributions = [];
                        tagContributions[tw.TagId] = contributions;
                    }
                    contributions.Add(contribution);

                    if (!tagStats.TryGetValue(tw.TagId, out var stats))
                        stats = (0, 0);
                    stats.Total++;
                    if (answer.IsCorrect == true)
                        stats.Correct++;
                    tagStats[tw.TagId] = stats;
                }
            }
        }

        var perTagResults = tagContributions
            .Select(kv =>
            {
                decimal topicScore = kv.Value.Count > 0
                    ? Math.Round(kv.Value.Average(), 2)
                    : 0m;

                var (correct, total) = tagStats.TryGetValue(kv.Key, out var s) ? s : (0, 0);

                return new TopicGradeResult
                {
                    TagId = kv.Key,
                    TopicScore = Math.Clamp(topicScore, 0.00m, 10.00m),
                    CorrectItems = correct,
                    TotalItems = total
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
            PerTagResults = perTagResults,
            Answers = gradedAnswers,
            GradedAt = DateTime.UtcNow
        };
    }

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

        var primary = questionTopics.FirstOrDefault(qt => qt.IsPrimary);
        var secondaries = questionTopics.Where(qt => !qt.IsPrimary).ToList();

        decimal wMain = PrimaryTagWeight;
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

    private static bool IsAbandoned(TestAnswer answer, string questionType)
    {
        var typeNormalized = questionType.Replace("_", "").Replace(" ", "").ToUpperInvariant();
        return typeNormalized switch
        {
            "SINGLECHOICE" or "TRUEFALSE" => answer.AnswerId is null,
            "MULTIPLESELECT" or "MULTIPLECHOICE" => answer.SelectedOptions.Count == 0,
            "SHORTANSWER" => string.IsNullOrWhiteSpace(answer.ShortAnswerText),
            "COMPOSITE" => answer.AnswerParts.Count == 0 || answer.AnswerParts.All(p =>
                p.BooleanAnswer is null && string.IsNullOrWhiteSpace(p.TextAnswer) && p.NumericAnswer is null),
            _ => true
        };
    }
}
