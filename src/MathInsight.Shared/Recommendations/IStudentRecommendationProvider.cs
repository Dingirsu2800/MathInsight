namespace MathInsight.Shared.Recommendations;

public interface IStudentRecommendationProvider
{
    Task<IReadOnlyList<WeakTagAdvice>> GetWeakTagAdviceAsync(
        string studentId,
        CancellationToken cancellationToken = default);
}

public sealed record WeakTagAdvice(
    string TagId,
    string TagName,
    decimal OfficialPoint,
    int EvidenceCount,
    byte RecommendedDifficultyLevel,
    string Reason);
