using MediatR;

namespace MathInsight.Modules.Recommender.Queries.GetRecommendedMaterials;

/// <summary>
/// MediatR query: returns recommended materials based on weak tags (UC-54, RCM-10).
/// Matches materials through LectureMaterial → Lecture.TagID to weak TagIDs;
/// remedial topics sorted first.
/// </summary>
public sealed record GetRecommendedMaterialsQuery(string StudentId)
    : IRequest<IReadOnlyList<RecommendedMaterialResponse>>;
