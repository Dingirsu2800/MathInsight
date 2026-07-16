namespace MathInsight.Shared.Recommendation;

/// <summary>
/// Stable in-process contract used by TestGen to read recommendation advice
/// without referencing the Recommender module directly.
/// </summary>
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
    bool IsWeak,
    byte RecommendedDifficultyLevel,
    bool IsRemedial,
    string Reason);
