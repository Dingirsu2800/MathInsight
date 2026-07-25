using MediatR;

namespace MathInsight.Modules.Recommender.Queries.GetRecommendedLectures;

/// <summary>
/// MediatR query: returns recommended lectures based on weak tags (UC-53, RCM-10).
/// Matches Lecture.TagID to weak TagIDs; remedial topics sorted first.
/// </summary>
public sealed record GetRecommendedLecturesQuery(string StudentId)
    : IRequest<IReadOnlyList<RecommendedLectureResponse>>;
