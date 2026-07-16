using MathInsight.Modules.Identity_Access.Contracts.Admin;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Identity_Access.Commands.UpdateRole;

public sealed record UpdateRoleCommand(
    string RoleId,
    string? RoleName,
    string? Description) : IRequest<Result<RoleResponse>>;
