using MediatR;

namespace MathInsight.Modules.Recommender.Queries.GetRecommendedLectures;

/// <summary>
/// MediatR query for difficulty-aware lecture recommendations (UC-53, RCM-10).
/// </summary>
public sealed record GetRecommendedLecturesQuery(string StudentId)
    : IRequest<IReadOnlyList<RecommendedLectureResponse>>;
