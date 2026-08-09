using MediatR;
using MathInsight.Modules.Recommender.Contracts;

namespace MathInsight.Modules.Recommender.Queries.GetAllTagsMastery;

/// <summary>
/// MediatR query: returns ALL topic mastery rows for the authenticated student (UC-55).
/// Unlike <see cref="GetWeakTags.GetWeakTagsQuery"/>, this query is not filtered by OfficialPoint.
/// </summary>
public sealed record GetAllTagsMasteryQuery(string StudentId)
    : IRequest<IReadOnlyList<TagMasteryDto>>;
