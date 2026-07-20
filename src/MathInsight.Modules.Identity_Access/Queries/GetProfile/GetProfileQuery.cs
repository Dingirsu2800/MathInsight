using MathInsight.Modules.Identity_Access.Contracts.Accounts;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Identity_Access.Queries.GetProfile;

/// <summary>
/// UC-04. Returns the caller's own profile. <paramref name="AccountId"/> is read from the
/// authenticated principal's claims by the controller — never from a request parameter, so one
/// user cannot read another's profile.
/// </summary>
public sealed record GetProfileQuery(string AccountId) : IRequest<Result<ProfileResponse>>;
