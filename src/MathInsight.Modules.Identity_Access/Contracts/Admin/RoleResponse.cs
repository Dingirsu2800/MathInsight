namespace MathInsight.Modules.Identity_Access.Contracts.Admin;

public sealed record RoleResponse(
    string RoleId,
    string RoleName,
    string? Description,
    IReadOnlyList<PermissionResponse> Permissions);

public sealed record PermissionResponse(
    string PermissionId,
    string PermissionKey,
    string? Description,
    bool IsGranted);
