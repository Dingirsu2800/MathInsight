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
        var grade = await _context.Students.AsNoTracking().Where(student => student.StudentId == query.StudentId).Select(student => student.CurrentGrade).FirstOrDefaultAsync(cancellationToken);
        if (grade is not (10 or 11 or 12)) return Result<TopicPracticeOptionsResponse>.Failure(TestGenerationErrors.TopicPracticeStudentNotFound);
        var studentGrade = grade.Value;
        var topics = await _context.TagTopics.AsNoTracking().Where(topic => topic.Grade == studentGrade && topic.IsActive).OrderBy(topic => topic.DisplayOrder).ThenBy(topic => topic.TagName).ToListAsync(cancellationToken);
        var recommendationResult = await _recommendationResolver.ResolveForTopicsAsync(query.StudentId, topics, cancellationToken);
        if (recommendationResult.IsFailure)
            return Result<TopicPracticeOptionsResponse>.Failure(recommendationResult.Error!);

        var recommendations = recommendationResult.Value!;
        var difficulties = await _context.TagDifficulties.AsNoTracking().Where(item => item.IsActive && item.LevelValue >= 1 && item.LevelValue <= 4).Select(item => item.DifficultyId).ToListAsync(cancellationToken);
        var pool = await _catalog.GetCandidatesAsync(new QuestionCandidateCatalogFilter(studentGrade, topics.Select(topic => topic.TagId).ToList(), difficulties, ["SingleChoice", "Composite", "ShortAnswer"]), cancellationToken);
        var candidates = pool.Candidates.Where(candidate => difficulties.Contains(candidate.DifficultyId, StringComparer.OrdinalIgnoreCase)).ToList();
        var response = topics.Select(topic =>
        {
            var subtree = TopicTreeResolver.ResolveActiveSubtree(topic.TagId, topics);
            var matching = candidates.Where(candidate => candidate.TagIds.Overlaps(subtree)).GroupBy(candidate => candidate.QuestionId, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToList();
            var count = matching.Count(candidate => !string.Equals(candidate.QuestionType, "Composite", StringComparison.OrdinalIgnoreCase)) + Math.Min(TopicPracticePolicy.MaxCompositeCount, matching.Count(candidate => string.Equals(candidate.QuestionType, "Composite", StringComparison.OrdinalIgnoreCase)));
            recommendations.TryGetValue(topic.TagId, out var recommendation);
            var advice = recommendation?.RepresentativeAdvice;
            return new TopicPracticeTopicResponse(
                topic.TagId,
                topic.ParentTagId,
                topic.TagName,
                topic.DisplayOrder,
                count,
                count >= TopicPracticePolicy.QuestionCount,
                recommendation?.IsAdaptive == true,
                advice?.TagId,
                advice?.TagName,
                advice?.OfficialPoint,
                advice?.EvidenceCount,
                advice?.RecommendedDifficultyLevel,
                advice?.Reason);
        }).ToList();
        return Result<TopicPracticeOptionsResponse>.Success(new TopicPracticeOptionsResponse(grade.Value, TopicPracticePolicy.QuestionCount, response));
    }
}
