using MathInsight.Modules.TestGen.Persistence.ReadModels;
using MathInsight.Shared.Results;

namespace MathInsight.Modules.TestGen.Generation;

public interface ITopicPracticeRecommendationResolver
{
    Task<Result<IReadOnlyDictionary<string, TopicPracticeRecommendationContext>>> ResolveForTopicsAsync(
        string studentId,
        IReadOnlyCollection<TagTopicReadModel> activeGradeTopics,
        CancellationToken cancellationToken);
}
