namespace MathInsight.Modules.Identity_Access.Errors;

public static class IdentityPermissionKeys
{
    /// <summary>
    /// Guard permission always attached to the Admin role. An admin cannot remove this
    /// permission from the Admin role while editing that same role (UC-16 self-protection rule).
    /// </summary>
    public const string AdminAccess = "identity:admin_access";
}
