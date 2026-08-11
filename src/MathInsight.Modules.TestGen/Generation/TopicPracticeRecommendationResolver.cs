using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Persistence.ReadModels;
using MathInsight.Shared.Recommendations;
using MathInsight.Shared.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MathInsight.Modules.TestGen.Generation;

public sealed class TopicPracticeRecommendationResolver : ITopicPracticeRecommendationResolver
{
    private readonly IStudentRecommendationProvider _provider;
    private readonly TopicPracticeFeatureOptions _options;
    private readonly ILogger<TopicPracticeRecommendationResolver> _logger;

    public TopicPracticeRecommendationResolver(
        IStudentRecommendationProvider provider,
        IOptions<TopicPracticeFeatureOptions> options,
        ILogger<TopicPracticeRecommendationResolver> logger)
    {
        _provider = provider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyDictionary<string, TopicPracticeRecommendationContext>>> ResolveForTopicsAsync(
        string studentId,
        IReadOnlyCollection<TagTopicReadModel> activeGradeTopics,
        CancellationToken cancellationToken)
    {
        var activeTopics = activeGradeTopics
            .Where(topic => topic.IsActive && !string.IsNullOrWhiteSpace(topic.TagId))
            .ToList();

        if (!_options.WeakTagAdaptiveEnabled)
            return Result<IReadOnlyDictionary<string, TopicPracticeRecommendationContext>>.Success(
                CreateBaselineContexts(activeTopics));

        IReadOnlyList<WeakTagAdvice> advice;
        try
        {
            advice = await _provider.GetWeakTagAdviceAsync(studentId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "TopicPractice recommendation provider failed for StudentId {StudentId}", studentId);
            return Result<IReadOnlyDictionary<string, TopicPracticeRecommendationContext>>.Failure(
                TestGenerationErrors.TopicPracticeRecommenderUnavailable);
        }

        if (!IsValidAdvice(advice))
        {
            _logger.LogWarning("TopicPractice recommendation provider returned invalid advice for StudentId {StudentId}", studentId);
            return Result<IReadOnlyDictionary<string, TopicPracticeRecommendationContext>>.Failure(
                TestGenerationErrors.TopicPracticeRecommendationInvalid);
        }

        var contexts = new Dictionary<string, TopicPracticeRecommendationContext>(StringComparer.OrdinalIgnoreCase);

        foreach (var selectedTopic in activeTopics)
        {
            var representative = advice
                .SingleOrDefault(item => string.Equals(item.TagId, selectedTopic.TagId, StringComparison.OrdinalIgnoreCase));

            contexts[selectedTopic.TagId] = representative is null
                ? TopicPracticeRecommendationContext.Baseline
                : new TopicPracticeRecommendationContext(
                    true,
                    representative,
                    new HashSet<string>([selectedTopic.TagId], StringComparer.OrdinalIgnoreCase));
        }

        return Result<IReadOnlyDictionary<string, TopicPracticeRecommendationContext>>.Success(contexts);
    }

    private static IReadOnlyDictionary<string, TopicPracticeRecommendationContext> CreateBaselineContexts(
        IReadOnlyCollection<TagTopicReadModel> activeTopics)
    {
        return activeTopics.ToDictionary(
            topic => topic.TagId,
            _ => TopicPracticeRecommendationContext.Baseline,
            StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsValidAdvice(IReadOnlyList<WeakTagAdvice> advice)
    {
        var tagIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return advice.All(item =>
            !string.IsNullOrWhiteSpace(item.TagId) &&
            tagIds.Add(item.TagId) &&
            !string.IsNullOrWhiteSpace(item.TagName) &&
            item.OfficialPoint >= 0m &&
            item.OfficialPoint < 5m &&
            item.EvidenceCount >= 3 &&
            item.RecommendedDifficultyLevel is 1 or 2 &&
            !string.IsNullOrWhiteSpace(item.Reason));
    }

}
