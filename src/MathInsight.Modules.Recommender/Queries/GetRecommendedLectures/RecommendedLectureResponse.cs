namespace MathInsight.Modules.Recommender.Queries.GetRecommendedLectures;

/// <summary>
/// A difficulty-aware, auditable lecture recommendation.
/// </summary>
public sealed record RecommendedLectureResponse(
    string LectureId,
    string Title,
    string? ThumbnailUrl,
    string TagId,
    string TagName,
    string DifficultyId,
    string DifficultyName,
    int DifficultyLevel,
    byte TargetDifficultyLevel,
    decimal? OfficialPoint,
    int EvidenceCount,
    int Likes,
    bool IsDifficultyFallback,
    string Reason);
