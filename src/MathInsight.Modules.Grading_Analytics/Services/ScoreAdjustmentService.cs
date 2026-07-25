using System.Data;
using System.Text.Json;
using MathInsight.Modules.Grading_Analytics.Persistence;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;
using MathInsight.Shared.Events;
using MathInsight.Shared.Questions;
using MathInsight.Shared.Scoring;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MathInsight.Modules.Grading_Analytics.Services;

public sealed class ScoreAdjustmentService : IScoreAdjustmentService
{
    private const decimal PrimaryTagWeight = 0.65m;

    private readonly GradingDbContext _db;
    private readonly IPublisher _publisher;

    public ScoreAdjustmentService(GradingDbContext db, IPublisher publisher)
    {
        _db = db;
        _publisher = publisher;
    }

    public async Task AdjustInvalidQuestionVersionAsync(
        string reportId,
        CancellationToken cancellationToken = default)
    {
        var events = new List<GradeCalculatedEvent>();
        var strategy = _db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async ct =>
        {
            events.Clear();
            await using IDbContextTransaction? transaction = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
                : null;

            var report = await _db.QuestionReports
                .FirstOrDefaultAsync(item => item.ReportId == reportId, ct)
                ?? throw new InvalidOperationException($"Question report '{reportId}' was not found.");

            if (report.Status != "Resolved" ||
                report.ReporterRole != "Student" ||
                report.ResolutionAction != "InvalidateAndAwardFull" ||
                string.IsNullOrWhiteSpace(report.QuestionVersionId))
            {
                throw new InvalidOperationException(
                    $"Question report '{reportId}' is not eligible for score adjustment.");
            }

            if (report.ScoreAdjustedTime is not null)
            {
                if (transaction is not null)
                    await transaction.CommitAsync(ct);
                return;
            }

            var versionTestQuestions = await _db.TestQuestions
                .Where(item => item.QuestionVersionId == report.QuestionVersionId)
                .ToListAsync(ct);

            var newlyInvalidated = versionTestQuestions
                .Where(item => !item.IsScoreInvalidated)
                .ToList();

            foreach (var testQuestion in newlyInvalidated)
            {
                testQuestion.IsScoreInvalidated = true;
                testQuestion.InvalidatedByReportId = report.ReportId;
            }

            var affectedTestIds = versionTestQuestions
                .Select(item => item.TestId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var testsNeedingRevision = newlyInvalidated
                .Select(item => item.TestId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (affectedTestIds.Count > 0)
            {
                var sessions = await _db.TestSessions
                    .Where(session => affectedTestIds.Contains(session.TestId) && session.Status == "Graded")
                    .Include(session => session.TestAnswers)
                        .ThenInclude(answer => answer.Question)
                            .ThenInclude(question => question.QuestionTopics)
                    .Include(session => session.TestAnswers)
                        .ThenInclude(answer => answer.SelectedOptions)
                    .Include(session => session.TestAnswers)
                        .ThenInclude(answer => answer.AnswerParts)
                    .ToListAsync(ct);

                var allTestQuestions = await _db.TestQuestions
                    .Where(item => affectedTestIds.Contains(item.TestId))
                    .Include(item => item.QuestionVersion)
                    .ToListAsync(ct);

                foreach (var session in sessions)
                {
                    var byQuestion = allTestQuestions
                        .Where(item => item.TestId == session.TestId)
                        .ToDictionary(item => item.QuestionId, StringComparer.OrdinalIgnoreCase);

                    RecalculateSession(session, byQuestion);
                    if (testsNeedingRevision.Contains(session.TestId))
                        session.GradeRevision = Math.Max(1, session.GradeRevision + 1);
                    events.Add(BuildGradeEvent(session, byQuestion));
                }
            }

            await _db.SaveChangesAsync(ct);
            if (transaction is not null)
                await transaction.CommitAsync(ct);
        }, cancellationToken);

        foreach (var gradeEvent in events)
            await _publisher.Publish(gradeEvent, cancellationToken);

        var adjustedReport = await _db.QuestionReports
            .FirstAsync(item => item.ReportId == reportId, cancellationToken);
        if (adjustedReport.ScoreAdjustedTime is null)
        {
            adjustedReport.ScoreAdjustedTime = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private static void RecalculateSession(
        TestSession session,
        IReadOnlyDictionary<string, TestQuestion> testQuestions)
    {
        var earned = 0m;
        var max = 0m;
        var correct = 0;
        var incorrect = 0;
        var abandoned = 0;

        foreach (var answer in session.TestAnswers)
        {
            if (!testQuestions.TryGetValue(answer.QuestionId, out var testQuestion))
                throw new InvalidOperationException($"Missing TestQuestion for '{answer.QuestionId}'.");

            max += testQuestion.MaxPointsSnapshot;
            earned += testQuestion.IsScoreInvalidated
                ? testQuestion.MaxPointsSnapshot
                : answer.PointsEarned;

            if (testQuestion.IsScoreInvalidated)
                continue;

            var snapshot = DeserializeSnapshot(testQuestion);
            if (IsAbandoned(answer, snapshot.QuestionType))
            {
                abandoned++;
                continue;
            }

            if (answer.IsCorrect == true)
                correct++;
            else
                incorrect++;
        }

        session.Score = max > 0m ? Math.Round(earned / max * 10m, 2) : 0m;
        session.NumCorrect = correct;
        session.NumIncorrect = incorrect;
        session.NumAbandoned = abandoned;
    }

    private static GradeCalculatedEvent BuildGradeEvent(
        TestSession session,
        IReadOnlyDictionary<string, TestQuestion> testQuestions)
    {
        var answers = new List<GradedAnswerDto>();
        var tagStats = new Dictionary<string, (decimal Correct, decimal Total, decimal Earned, decimal Max)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var answer in session.TestAnswers)
        {
            var testQuestion = testQuestions[answer.QuestionId];
            var snapshot = DeserializeSnapshot(testQuestion);
            var tagWeights = BuildTagWeights(snapshot);
            var tagId = tagWeights.FirstOrDefault(item => item.IsPrimary)?.TagId
                ?? tagWeights.FirstOrDefault()?.TagId
                ?? string.Empty;
            var invalidated = testQuestion.IsScoreInvalidated;
            var earned = invalidated ? testQuestion.MaxPointsSnapshot : answer.PointsEarned;
            var isAbandoned = !invalidated && IsAbandoned(answer, snapshot.QuestionType);

            answers.Add(new GradedAnswerDto
            {
                QuestionId = answer.QuestionId,
                TagId = tagId,
                TagWeights = tagWeights,
                NormalizedScore = testQuestion.MaxPointsSnapshot > 0m
                    ? Math.Round(earned / testQuestion.MaxPointsSnapshot * 10m, 2)
                    : 0m,
                IsCorrect = invalidated || answer.IsCorrect == true,
                MachineIsCorrect = answer.IsCorrect,
                IsScoreInvalidated = invalidated,
                PointsEarned = earned,
                MaxPoints = testQuestion.MaxPointsSnapshot,
                TimeSpent = answer.TimeSpent ?? 0,
                DifficultyLevel = 1,
                QuestionNo = answer.QuestionNo,
                IsAbandoned = isAbandoned
            });

            foreach (var tagWeight in tagWeights)
            {
                tagStats.TryGetValue(tagWeight.TagId, out var stats);
                if (!invalidated)
                {
                    stats.Total++;
                    if (answer.IsCorrect == true)
                        stats.Correct++;
                    stats.Earned += answer.PointsEarned * tagWeight.Weight;
                    stats.Max += testQuestion.MaxPointsSnapshot * tagWeight.Weight;
                }
                tagStats[tagWeight.TagId] = stats;
            }
        }

        return new GradeCalculatedEvent
        {
            SessionId = session.SessionId,
            StudentId = session.StudentId,
            TestId = session.TestId,
            GradeRevision = session.GradeRevision,
            TestFormat = session.TestFormat,
            Score = session.Score,
            NumCorrect = session.NumCorrect,
            NumIncorrect = session.NumIncorrect,
            NumAbandoned = session.NumAbandoned,
            Answers = answers,
            PerTagResults = tagStats.Select(item => new TopicGradeResult
            {
                TagId = item.Key,
                TotalItems = item.Value.Total,
                CorrectItems = item.Value.Correct,
                EarnedPoints = item.Value.Earned,
                MaxPoints = item.Value.Max,
                TopicScore = item.Value.Max > 0m
                    ? Math.Round(item.Value.Earned / item.Value.Max * 10m, 2)
                    : 0m
            }).ToList(),
            GradedAt = DateTime.UtcNow
        };
    }

    private static IReadOnlyList<TagWeightEntry> BuildTagWeights(QuestionSnapshotV2 snapshot)
    {
        var topics = snapshot.Topics.ToList();
        if (topics.Count == 0)
            return [];
        if (topics.Count == 1)
        {
            return [new TagWeightEntry
            {
                TagId = topics[0].TagId,
                Weight = 1m,
                IsPrimary = true
            }];
        }

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

    private static QuestionSnapshotV2 DeserializeSnapshot(TestQuestion testQuestion)
    {
        if (testQuestion.QuestionVersion.SnapshotSchemaVersion != 2)
            throw new InvalidOperationException(
                $"Unsupported snapshot schema for version '{testQuestion.QuestionVersionId}'.");

        return JsonSerializer.Deserialize<QuestionSnapshotV2>(testQuestion.QuestionVersion.AnswersSnapshot)
            ?? throw new InvalidOperationException(
                $"Invalid snapshot JSON for version '{testQuestion.QuestionVersionId}'.");
    }

    private static bool IsAbandoned(TestAnswer answer, string questionType)
    {
        var normalized = questionType.Replace("_", string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
        return normalized switch
        {
            "SINGLECHOICE" or "TRUEFALSE" => answer.AnswerId is null,
            "MULTIPLESELECT" or "MULTIPLECHOICE" => answer.SelectedOptions.Count == 0,
            "SHORTANSWER" => string.IsNullOrWhiteSpace(answer.ShortAnswerText),
            "COMPOSITE" => answer.AnswerParts.Count == 0 || answer.AnswerParts.All(part =>
                part.BooleanAnswer is null && string.IsNullOrWhiteSpace(part.TextAnswer) && part.NumericAnswer is null),
            _ => true
        };
    }
}
