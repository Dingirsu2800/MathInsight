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
    int EvidenceItemCount,
    int EvidenceSessionCount,
    byte RecommendedDifficultyLevel)
{
    public TopicMasteryAdvice(
        string tagId,
        decimal officialPoint,
        int evidenceItemCount,
        byte recommendedDifficultyLevel)
        : this(tagId, officialPoint, evidenceItemCount, 0, recommendedDifficultyLevel)
    {
    }
}
