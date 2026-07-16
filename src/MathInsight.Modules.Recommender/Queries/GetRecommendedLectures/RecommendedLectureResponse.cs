namespace MathInsight.Modules.Recommender.Queries.GetRecommendedLectures;

/// <summary>
/// Response DTO for recommended lectures.
/// </summary>
public sealed record RecommendedLectureResponse(
    string LectureId,
    string Title,
    string? Description,
    string TagId,
    string TagName,
    decimal OfficialPoint,
    bool IsRemedial);
