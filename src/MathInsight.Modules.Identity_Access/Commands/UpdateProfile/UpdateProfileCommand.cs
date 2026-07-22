using MathInsight.Modules.Identity_Access.Contracts.Accounts;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Identity_Access.Commands.UpdateProfile;

/// <summary>
/// UC-05. Partially updates the caller's own profile: every field below is optional, and a null
/// means "leave the stored value alone". <paramref name="AccountId"/> comes from the
/// authenticated principal's claims, not the request body.
///
/// Username and email are deliberately not part of this command and cannot be changed through it.
/// </summary>
public sealed record UpdateProfileCommand(
    string AccountId,
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    DateOnly? DateOfBirth,
    string? Gender,
    string? School,
    int? CurrentGrade,
    string? Biography,
    string? Specialty) : IRequest<Result<ProfileResponse>>;
