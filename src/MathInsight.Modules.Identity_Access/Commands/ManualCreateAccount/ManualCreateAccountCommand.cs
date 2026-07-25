using MathInsight.Modules.Identity_Access.Contracts.Admin;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Identity_Access.Commands.ManualCreateAccount;

public sealed record ManualCreateAccountCommand(
    string Username,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    DateOnly? DateOfBirth,
    string RoleName) : IRequest<Result<AccountListItemResponse>>;
