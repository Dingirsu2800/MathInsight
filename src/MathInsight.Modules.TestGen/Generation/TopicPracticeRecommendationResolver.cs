using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Persistence.ReadModels;
using MathInsight.Shared.Recommendations;
using MathInsight.Shared.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MathInsight.Modules.TestGen.Generation;

public sealed class TopicPracticeRecommendationResolver : ITopicPracticeRecommendationResolver
{
    private readonly IStudentTopicMasteryProvider _provider;
    private readonly TopicPracticeFeatureOptions _options;
    private readonly ILogger<TopicPracticeRecommendationResolver> _logger;

    public TopicPracticeRecommendationResolver(
        IStudentTopicMasteryProvider provider,
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

        IReadOnlyDictionary<string, TopicMasteryAdvice> advice;
        try
        {
            advice = await _provider.GetTopicMasteryAdviceAsync(
                studentId,
                activeTopics.Select(topic => topic.TagId).ToList(),
                cancellationToken);
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

        if (!IsValidAdvice(advice, activeTopics))
        {
            _logger.LogWarning("TopicPractice recommendation provider returned invalid advice for StudentId {StudentId}", studentId);
            return Result<IReadOnlyDictionary<string, TopicPracticeRecommendationContext>>.Failure(
                TestGenerationErrors.TopicPracticeRecommendationInvalid);
        }

        var contexts = new Dictionary<string, TopicPracticeRecommendationContext>(StringComparer.OrdinalIgnoreCase);

        foreach (var selectedTopic in activeTopics)
        {
            advice.TryGetValue(selectedTopic.TagId, out var representative);

            contexts[selectedTopic.TagId] = representative is null || representative.EvidenceItemCount < 3
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

    private static bool IsValidAdvice(
        IReadOnlyDictionary<string, TopicMasteryAdvice> advice,
        IReadOnlyCollection<TagTopicReadModel> activeTopics)
    {
        var activeTagIds = activeTopics
            .Select(topic => topic.TagId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return advice.All(pair =>
            !string.IsNullOrWhiteSpace(pair.Key) &&
            activeTagIds.Contains(pair.Key) &&
            string.Equals(pair.Key, pair.Value.TagId, StringComparison.OrdinalIgnoreCase) &&
            pair.Value.OfficialPoint is >= 0m and <= 10m &&
            pair.Value.EvidenceItemCount >= 0 &&
            pair.Value.RecommendedDifficultyLevel is >= 1 and <= 4);
    }

}
