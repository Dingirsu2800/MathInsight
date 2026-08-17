namespace MathInsight.Shared.Recommendations;

public interface IStudentTopicMasteryProvider
{
    Task<IReadOnlyDictionary<string, TopicMasteryAdvice>> GetTopicMasteryAdviceAsync(
        string studentId,
        IReadOnlyCollection<string> tagIds,
        CancellationToken cancellationToken = default);
}

public sealed record TopicMasteryAdvice(
    string TagId,
    decimal OfficialPoint,
    int EvidenceCount,
    byte RecommendedDifficultyLevel);
