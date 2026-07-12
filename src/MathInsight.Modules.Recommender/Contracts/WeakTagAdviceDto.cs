namespace MathInsight.Modules.Recommender.Contracts;

/// <summary>
/// Detailed weak-tag advisory returned by <see cref="Services.IRecommenderService.GetStudentWeakTagAdviceAsync"/>.
/// Used by TestGen to select questions at the recommended difficulty level.
///
/// <b>Cross-module contract</b>: <see cref="RecommendedDifficultyLevel"/> is a level integer (1–4),
/// NOT a difficulty_id (PK of TagDifficulty). Consumers must resolve via:
/// <c>SELECT DifficultyID FROM TagDifficulty WHERE LevelValue = @RecommendedDifficultyLevel</c>
/// </summary>
public sealed record WeakTagAdviceDto(
    Guid TagId,
    string TagName,
    decimal OfficialPoint,
    bool IsWeak,
    byte RecommendedDifficultyLevel,
    bool IsRemedial,
    string Reason);
