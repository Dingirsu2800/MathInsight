namespace MathInsight.Modules.Recommender.Queries.GetRecommendedLectures;

/// <summary>
/// Response DTO for recommended lectures.
/// </summary>
public sealed record RecommendedLectureResponse(
    Guid LectureId,
    string Title,
    string? Description,
    Guid TagId,
    string TagName,
    decimal OfficialPoint,
    bool IsRemedial);
