using System.Data;
using System.Text.Json;
using MathInsight.Modules.Recommender.Persistence;
using MathInsight.Modules.Recommender.Persistence.Entities;
using MathInsight.Modules.Recommender.Services;
using MathInsight.Shared.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Recommender.Handlers;

/// <summary>
/// Applies one graded session to per-topic mastery and audit snapshots.
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

        if (_db.Database.IsRelational())
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                // A retried transaction must not reuse Added/Modified state from a rolled-back attempt.
                _db.ChangeTracker.Clear();
                await using var transaction = await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
                await ProcessEventAsync(notification, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            });
            return;
        }

        await ProcessEventAsync(notification, cancellationToken);
    }

    private async Task ProcessEventAsync(
        GradeCalculatedEvent notification,
        CancellationToken cancellationToken)
    {
        var changed = false;
        foreach (var tagResult in notification.PerTagResults
                     .Where(result => !string.IsNullOrWhiteSpace(result.TagId))
                     .DistinctBy(result => result.TagId))
        {
            changed |= await IngestTopicResultAsync(notification, tagResult, cancellationToken);
        }

        if (!changed)
            return;

        await _db.SaveChangesAsync(cancellationToken);
        await _competencyEngine.RecalculateAsync(notification.StudentId, cancellationToken);
    }

    private async Task<bool> IngestTopicResultAsync(
        GradeCalculatedEvent evt,
        TopicGradeResult tagResult,
        CancellationToken cancellationToken)
    {
        var alreadyIngested = await _db.StudentTopicSessionResults
            .AnyAsync(
                result => result.SessionId == evt.SessionId && result.TagId == tagResult.TagId,
                cancellationToken);

        if (alreadyIngested)
            return false;

        var mastery = await _db.TagsMasteries
            .FirstOrDefaultAsync(
                item => item.StudentId == evt.StudentId && item.TagId == tagResult.TagId,
                cancellationToken);

        if (mastery is null)
        {
            mastery = new TagsMastery
            {
                TagsMasteryId = Guid.NewGuid().ToString("D"),
                StudentId = evt.StudentId,
                TagId = tagResult.TagId,
                OfficialPoint = 5.00m,
                PracticePoint = 5.00m,
                ExamAnchor = 5.00m,
                ExamHistory = "[]",
                SeriesAnswerCount = 0,
                RecommendedDifficultyLevel = 3,
                MasteryStatus = "NotLearned",
                NumberDone = 0,
                NumCorrect = 0,
                AccuracyRate = 0.00m
            };
            _db.TagsMasteries.Add(mastery);
        }

        var tagAnswers = evt.Answers
            .Where(answer => answer.TagId == tagResult.TagId)
            .OrderBy(answer => answer.QuestionNo)
            .ToList();

        if (string.Equals(evt.TestFormat, "Exam", StringComparison.OrdinalIgnoreCase))
        {
            var history = DeserializeHistory(mastery.ExamHistory);
            history.Insert(0, tagResult.TopicScore);
            if (history.Count > MaxExamHistory)
                history.RemoveAt(history.Count - 1);

            mastery.ExamAnchor = CalculateExamAnchor(history);
            mastery.ExamHistory = JsonSerializer.Serialize(history);
            mastery.OfficialPoint = CalculateOfficialPoint(mastery.ExamAnchor, mastery.PracticePoint);
        }
        else if (string.Equals(evt.TestFormat, "Practice", StringComparison.OrdinalIgnoreCase))
        {
            ApplyPracticeAnswers(mastery, tagAnswers);
            mastery.LastPracticedTime = evt.GradedAt;
        }

        var totalCount = tagAnswers.Count > 0
            ? tagAnswers.Count
            : ToWholeItemCount(tagResult.TotalItems);
        var correctCount = tagAnswers.Count > 0
            ? tagAnswers.Count(answer => answer.IsCorrect)
            : ToWholeItemCount(tagResult.CorrectItems);

        mastery.NumberDone += totalCount;
        mastery.NumCorrect += correctCount;
        mastery.AccuracyRate = mastery.NumberDone > 0
            ? Math.Round((decimal)mastery.NumCorrect / mastery.NumberDone * 100m, 2)
            : 0.00m;
        mastery.RecommendedDifficultyLevel = MapDifficultyLevel(mastery.OfficialPoint);
        mastery.MasteryStatus = DetermineMasteryStatus(mastery.NumberDone, mastery.OfficialPoint);
        mastery.LastCalculatedAt = evt.GradedAt;

        _db.StudentTopicSessionResults.Add(new StudentTopicSessionResult
        {
            StudentTopicSessionResultId = Guid.NewGuid().ToString("D"),
            StudentId = evt.StudentId,
            SessionId = evt.SessionId,
            TagId = tagResult.TagId,
            TotalItems = tagResult.TotalItems,
            CorrectItems = tagResult.CorrectItems,
            EarnedPoints = tagResult.EarnedPoints,
            MaxPoints = tagResult.MaxPoints,
            TopicScore = tagResult.TopicScore,
            CreatedTime = evt.GradedAt
        });

        return true;
    }

    private static void ApplyPracticeAnswers(TagsMastery mastery, IEnumerable<GradedAnswerDto> answers)
    {
        foreach (var answer in answers)
        {
            mastery.SeriesAnswerCount++;

            var difficultyWeight = answer.DifficultyLevel switch
            {
                1 => 0.5m,
                2 => 1.0m,
                3 => 1.5m,
                4 => 2.0m,
                _ => 1.0m
            };
            var timePenalty = answer.TimeSpent < 5 && !answer.IsAbandoned ? 1.5m : 1.0m;
            var delta = answer.IsCorrect
                ? 0.05m * difficultyWeight
                : -0.05m * (5.0m - difficultyWeight) * timePenalty;

            mastery.PracticePoint = Math.Clamp(mastery.PracticePoint + delta, 0.00m, 10.00m);
            mastery.OfficialPoint = CalculateOfficialPoint(mastery.ExamAnchor, mastery.PracticePoint);

            if (mastery.SeriesAnswerCount >= 10)
            {
                mastery.PracticePoint = mastery.OfficialPoint;
                mastery.SeriesAnswerCount = 0;
            }
        }
    }

    private static decimal CalculateOfficialPoint(decimal examAnchor, decimal practicePoint)
        => Math.Clamp(0.7m * examAnchor + 0.3m * practicePoint, 0.00m, 10.00m);

    private static decimal CalculateExamAnchor(IEnumerable<decimal> history)
    {
        decimal weightedSum = 0m;
        decimal weightSum = 0m;
        decimal weight = 1m;

        foreach (var score in history)
        {
            weightedSum += weight * score;
            weightSum += weight;
            weight *= Beta;
        }

        return weightSum == 0m
            ? 5.00m
            : Math.Clamp(weightedSum / weightSum, 0.00m, 10.00m);
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
        if (numberDone == 0)
            return "NotLearned";
        return officialPoint >= MasteredThreshold ? "Mastered" : "Learning";
    }

    private static int ToWholeItemCount(decimal value)
    {
        if (value <= 0m)
            return 0;
        return decimal.ToInt32(decimal.Truncate(value));
    }

    private static List<decimal> DeserializeHistory(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<decimal>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
