namespace MathInsight.Modules.Recommender.Contracts;

/// <summary>
/// Detailed weak-tag advisory returned by <see cref="Services.IRecommenderService.GetStudentWeakTagAdviceAsync"/>.
/// Used by TestGen to select questions at the recommended difficulty level.
/// </summary>
public sealed record WeakTagAdviceDto(
    string TagId,
    string TagName,
    decimal OfficialPoint,
    bool IsWeak,
    byte RecommendedDifficultyLevel,
    bool IsRemedial,
    string Reason,
    string? RecommendedDifficultyId = null);
