using System.Text.Json;
using MathInsight.Modules.Grading_Analytics.Persistence;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;
using MathInsight.Shared.Events;
using MathInsight.Shared.Questions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MathInsight.Modules.Grading_Analytics.Services;

public sealed class GradingOrchestrator : IGradingOrchestrator
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
            _logger.LogError(
                exception,
                "Grading failed for session {SessionId}; the transaction was rolled back.",
                sessionId);
            throw;
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
            if (!testQuestions.TryGetValue(answer.QuestionId, out var testQuestion))
            {
                if (_db.Database.IsRelational())
                    throw new InvalidOperationException(
                        $"Missing TestQuestion snapshot for question '{answer.QuestionId}'.");
                continue;
            }

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

        await _db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return BuildGradeCalculatedEvent(session, gradingResult);
    }

    private static GradeCalculatedEvent BuildGradeCalculatedEvent(
        TestSession session,
        GradingResult gradingResult)
    {
        var answers = new List<GradedAnswerDto>();
        var tagStats = new Dictionary<string, TagStat>(StringComparer.OrdinalIgnoreCase);

        foreach (var answer in session.TestAnswers)
        {
            var snapshot = answer.Snapshot;
            var questionType = snapshot?.QuestionType ?? answer.Question.QuestionType;
            var maxPoints = snapshot is null
                ? answer.Question.DefaultWeight
                : answer.MaxPointsSnapshot;
            var effectivePoints = answer.IsScoreInvalidated ? maxPoints : answer.PointsEarned;
            var tagWeights = BuildTagWeights(snapshot, answer.Question);
            var primaryTagId = tagWeights.FirstOrDefault(item => item.IsPrimary)?.TagId
                ?? tagWeights.FirstOrDefault()?.TagId
                ?? string.Empty;

            answers.Add(new GradedAnswerDto
            {
                QuestionId = answer.QuestionId,
                TagId = primaryTagId,
                TagWeights = tagWeights,
                NormalizedScore = maxPoints > 0m
                    ? Math.Round(effectivePoints / maxPoints * 10m, 2)
                    : 0m,
                IsCorrect = answer.IsScoreInvalidated || answer.IsCorrect == true,
                MachineIsCorrect = answer.IsCorrect,
                IsScoreInvalidated = answer.IsScoreInvalidated,
                PointsEarned = effectivePoints,
                MaxPoints = maxPoints,
                TimeSpent = answer.TimeSpent ?? 0,
                DifficultyLevel = 1,
                QuestionNo = answer.QuestionNo,
                IsAbandoned = !answer.IsScoreInvalidated &&
                    GradingEngine.IsAbandoned(answer, questionType)
            });

            foreach (var tagWeight in tagWeights)
            {
                tagStats.TryGetValue(tagWeight.TagId, out var stat);
                if (!answer.IsScoreInvalidated)
                {
                    stat.TotalItems++;
                    if (answer.IsCorrect == true)
                        stat.CorrectItems++;
                    stat.EarnedPoints += answer.PointsEarned * tagWeight.Weight;
                    stat.MaxPoints += maxPoints * tagWeight.Weight;
                }
                tagStats[tagWeight.TagId] = stat;
            }
        }

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
            Answers = answers,
            PerTagResults = tagStats.Select(item => new TopicGradeResult
            {
                TagId = item.Key,
                TotalItems = item.Value.TotalItems,
                CorrectItems = item.Value.CorrectItems,
                EarnedPoints = item.Value.EarnedPoints,
                MaxPoints = item.Value.MaxPoints,
                TopicScore = item.Value.MaxPoints > 0m
                    ? Math.Round(item.Value.EarnedPoints / item.Value.MaxPoints * 10m, 2)
                    : 0m
            }).ToList(),
            GradedAt = DateTime.UtcNow
        };
    }

    private static IReadOnlyList<TagWeightEntry> BuildTagWeights(
        QuestionSnapshotV2? snapshot,
        Question question)
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
