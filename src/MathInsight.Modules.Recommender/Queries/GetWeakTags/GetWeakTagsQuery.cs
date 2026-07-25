using MediatR;
using MathInsight.Modules.Recommender.Contracts;

namespace MathInsight.Modules.Recommender.Queries.GetWeakTags;

/// <summary>
/// MediatR query: returns weak topics for the authenticated student (UC-52).
/// </summary>
public sealed record GetWeakTagsQuery(string StudentId)
    : IRequest<IReadOnlyList<WeakTagDto>>;
