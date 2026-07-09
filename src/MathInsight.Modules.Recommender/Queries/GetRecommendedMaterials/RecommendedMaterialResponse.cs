namespace MathInsight.Modules.Recommender.Queries.GetRecommendedMaterials;

/// <summary>
/// Response DTO for recommended materials.
/// Materials are linked to lectures through the LectureMaterial join table (many-to-many).
/// </summary>
public sealed record RecommendedMaterialResponse(
    Guid MaterialId,
    string Title,
    string? Description,
    string? FileUrl,
    string? MaterialType,
    Guid TagId,
    string TagName,
    decimal OfficialPoint,
    bool IsRemedial);
