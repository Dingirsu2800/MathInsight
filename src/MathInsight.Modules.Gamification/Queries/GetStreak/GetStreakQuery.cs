using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Gamification.Queries.GetStreak;

/// <summary>
/// UC-81. Reads the caller's own study streak. <paramref name="StudentId"/> comes from the
/// authenticated principal's JWT claims (set by the controller), never from the request body —
/// mirrors GetProfileQuery.
/// </summary>
public sealed record GetStreakQuery(string StudentId) : IRequest<Result<StreakResponse>>;
