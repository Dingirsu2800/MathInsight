using MathInsight.Modules.Identity_Access.Contracts.Admin;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Identity_Access.Commands.AdjustPermission;

public sealed record AdjustPermissionCommand(
    string RoleId,
    IReadOnlyList<string> PermissionIds,
    string RequestingAccountId) : IRequest<Result<RoleResponse>>;
