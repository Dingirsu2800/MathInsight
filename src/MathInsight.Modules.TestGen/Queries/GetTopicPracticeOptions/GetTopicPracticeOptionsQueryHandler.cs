using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Generation;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.TestGen.Queries.GetTopicPracticeOptions;

public sealed class GetTopicPracticeOptionsQueryHandler : IRequestHandler<GetTopicPracticeOptionsQuery, Result<TopicPracticeOptionsResponse>>
{
    private readonly TestGenDbContext _context;
    private readonly IQuestionCandidateCatalog _catalog;
    private readonly ITopicPracticeRecommendationResolver _recommendationResolver;

    public GetTopicPracticeOptionsQueryHandler(
        TestGenDbContext context,
        IQuestionCandidateCatalog catalog,
        ITopicPracticeRecommendationResolver recommendationResolver)
    {
        _context = context;
        _catalog = catalog;
        _recommendationResolver = recommendationResolver;
    }

    public async Task<Result<TopicPracticeOptionsResponse>> Handle(GetTopicPracticeOptionsQuery query, CancellationToken cancellationToken)
    {
        var student = await _context.Students.AsNoTracking()
            .FirstOrDefaultAsync(item => item.StudentId == query.StudentId, cancellationToken);
        if (student is null)
            return Result<TopicPracticeOptionsResponse>.Failure(TestGenerationErrors.TopicPracticeStudentNotFound);
        if (student.CurrentGrade is not (10 or 11 or 12))
            return Result<TopicPracticeOptionsResponse>.Failure(TestGenerationErrors.StudentGradeRequired);

        var studentGrade = student.CurrentGrade.Value;
        var allTopics = await _context.TagTopics.AsNoTracking()
            .Where(topic => topic.Grade <= studentGrade && topic.IsActive)
            .OrderBy(topic => topic.DisplayOrder)
            .ThenBy(topic => topic.TagName)
            .ToListAsync(cancellationToken);
        var topicsById = allTopics.ToDictionary(topic => topic.TagId, StringComparer.OrdinalIgnoreCase);
        var topics = allTopics
            .Where(topic => IsAssignableDirectChild(topic, topicsById))
            .ToList();
        var recommendationResult = await _recommendationResolver.ResolveForTopicsAsync(query.StudentId, topics, cancellationToken);
        if (recommendationResult.IsFailure)
            return Result<TopicPracticeOptionsResponse>.Failure(recommendationResult.Error!);

        var recommendations = recommendationResult.Value!;
        var difficulties = await _context.TagDifficulties.AsNoTracking()
            .Where(item => item.IsActive && item.LevelValue >= 1 && item.LevelValue <= 4)
            .OrderBy(item => item.LevelValue)
            .ThenBy(item => item.DisplayOrder)
            .ThenBy(item => item.DifficultyName)
            .ToListAsync(cancellationToken);
        var difficultyIds = difficulties.Select(item => item.DifficultyId).ToList();
        var candidatesByTopic = topics.ToDictionary(
            topic => topic.TagId,
            _ => new Dictionary<string, BlueprintExamCandidate>(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
        foreach (var gradeGroup in topics.GroupBy(topic => topic.Grade))
        {
            var topicIds = gradeGroup.Select(topic => topic.TagId).ToList();
            var pool = await _catalog.GetCandidatesAsync(
                new QuestionCandidateCatalogFilter(gradeGroup.Key, topicIds, difficultyIds, ["SingleChoice", "Composite", "ShortAnswer"]),
                cancellationToken);
            var groupTopicIds = topicIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in pool.Candidates)
            {
                foreach (var tagId in candidate.TagIds.Where(groupTopicIds.Contains))
                    candidatesByTopic[tagId][candidate.QuestionId] = candidate;
            }
        }

        var response = new List<TopicPracticeTopicResponse>(topics.Count);
        foreach (var topic in topics)
        {
            var matching = candidatesByTopic[topic.TagId].Values.ToList();
            var count = matching.Count(candidate => !string.Equals(candidate.QuestionType, "Composite", StringComparison.OrdinalIgnoreCase)) + Math.Min(TopicPracticePolicy.MaxCompositeCount, matching.Count(candidate => string.Equals(candidate.QuestionType, "Composite", StringComparison.OrdinalIgnoreCase)));
            var availability = difficulties
                .Select(difficulty =>
                {
                    var candidatesAtDifficulty = matching
                        .Where(candidate => string.Equals(candidate.DifficultyId, difficulty.DifficultyId, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    var availableCount = candidatesAtDifficulty.Count(candidate =>
                        !string.Equals(candidate.QuestionType, "Composite", StringComparison.OrdinalIgnoreCase)) +
                        Math.Min(
                            TopicPracticePolicy.MaxCompositeCount,
                            candidatesAtDifficulty.Count(candidate => string.Equals(candidate.QuestionType, "Composite", StringComparison.OrdinalIgnoreCase)));
                    return new TopicPracticeDifficultyAvailabilityResponse(
                        difficulty.DifficultyId,
                        difficulty.DifficultyName,
                        checked((byte)difficulty.LevelValue),
                        availableCount,
                        availableCount >= TopicPracticePolicy.QuestionCount);
                })
                .ToList();
            recommendations.TryGetValue(topic.TagId, out var recommendation);
            var advice = recommendation?.RepresentativeAdvice;
            var parent = topicsById[topic.ParentTagId!];
            response.Add(new TopicPracticeTopicResponse(
                topic.TagId,
                topic.ParentTagId,
                parent.TagName,
                topic.TagName,
                topic.Grade,
                topic.DisplayOrder,
                count,
                count >= TopicPracticePolicy.QuestionCount,
                recommendation?.IsAdaptive == true,
                advice?.TagId,
                advice?.TagName,
                advice?.OfficialPoint,
                advice?.EvidenceCount,
                advice?.RecommendedDifficultyLevel,
                advice?.Reason,
                availability));
        }
        return Result<TopicPracticeOptionsResponse>.Success(new TopicPracticeOptionsResponse(studentGrade, TopicPracticePolicy.QuestionCount, response));
    }

    private static bool IsAssignableDirectChild(
        Persistence.ReadModels.TagTopicReadModel topic,
        IReadOnlyDictionary<string, Persistence.ReadModels.TagTopicReadModel> topicsById)
    {
        return !string.IsNullOrWhiteSpace(topic.ParentTagId) &&
            topicsById.TryGetValue(topic.ParentTagId, out var parent) &&
            parent.IsActive &&
            string.IsNullOrWhiteSpace(parent.ParentTagId) &&
            parent.Grade == topic.Grade;
    }
}
