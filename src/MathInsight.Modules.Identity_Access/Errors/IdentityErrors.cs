using MathInsight.Shared.Results;

namespace MathInsight.Modules.Identity_Access.Errors;

public static class IdentityErrors
{
    public static readonly Error AccountNotFound = new(
        "ACCOUNT_NOT_FOUND",
        "Account not found.");

    public static readonly Error EmailAlreadyExists = new(
        "EMAIL_ALREADY_EXISTS",
        "Email is already in use.");

    public static readonly Error UsernameAlreadyExists = new(
        "USERNAME_ALREADY_EXISTS",
        "Username is already in use.");

    public static readonly Error PasswordTooShort = new(
        "PASSWORD_TOO_SHORT",
        "Password must be at least 8 characters long.");

    public static readonly Error RoleNotFound = new(
        "ROLE_NOT_FOUND",
        "Role not found.");

    public static readonly Error RoleNameAlreadyExists = new(
        "ROLE_NAME_ALREADY_EXISTS",
        "Role name is already in use.");

    public static readonly Error PermissionNotFound = new(
        "PERMISSION_NOT_FOUND",
        "One or more permissions were not found.");

    public static readonly Error ApplicationNotFound = new(
        "APPLICATION_NOT_FOUND",
        "Teacher application not found.");

    public static readonly Error ApplicationAlreadyResolved = new(
        "APPLICATION_ALREADY_RESOLVED",
        "This application has already been resolved.");

    public static readonly Error RejectReasonRequired = new(
        "REJECT_REASON_REQUIRED",
        "A review comment is required when rejecting an application.");

    public static readonly Error CannotDeactivateSelf = new(
        "CANNOT_DEACTIVATE_SELF",
        "Admin cannot deactivate their own account.");

    public static readonly Error CannotRemoveOwnAdminPermission = new(
        "CANNOT_REMOVE_OWN_ADMIN_PERMISSION",
        "Admin cannot remove their own admin permission.");

    public static readonly Error SystemRoleRenameForbidden = new(
        "SYSTEM_ROLE_RENAME_FORBIDDEN",
        "System roles (Admin, Expert, Teacher, Student) cannot be renamed.");

    public static readonly Error InvalidExcelFile = new(
        "INVALID_EXCEL_FILE",
        "The uploaded file is not a valid Excel (.xlsx) file.");

    public static readonly Error InvalidRole = new(
        "INVALID_ROLE",
        "Role must be one of: Student, Teacher, Expert.");
}
