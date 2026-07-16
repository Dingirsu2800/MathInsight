using MathInsight.Modules.Identity_Access.Contracts.Admin;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Identity_Access.Commands.ToggleAccountStatus;

public sealed record ToggleAccountStatusCommand(
    string AccountId,
    bool IsActive,
    string RequestingAccountId) : IRequest<Result<AccountListItemResponse>>;
