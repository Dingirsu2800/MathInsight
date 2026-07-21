namespace MathInsight.Modules.Identity_Access.Contracts.Admin;

public sealed record AccountListItemResponse(
    string AccountId,
    string Username,
    string Email,
    string FirstName,
    string LastName,
    string RoleId,
    string RoleName,
    bool IsActive,
    DateTime CreatedTime);
