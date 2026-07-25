using MathInsight.Modules.Identity_Access.Contracts.Admin;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Identity_Access.Commands.UpdateAccount;

public sealed record UpdateAccountCommand(
    string AccountId,
    string FirstName,
    string LastName,
    string Email,
    string RoleId) : IRequest<Result<AccountListItemResponse>>;
