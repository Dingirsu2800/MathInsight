namespace MathInsight.Modules.Identity_Access.Contracts;

/// <summary>
/// Stable, machine-readable authentication error codes (BR-11). Frontend clients
/// localize these codes into Vietnamese, so the values must not change casually.
/// These replace the inline string literals previously scattered in AuthController.
/// There is deliberately no AUTH_EMAIL_NOT_CONFIRMED — per DD-01 that state cannot occur.
/// </summary>
public static class AuthErrorCodes
{
    /// <summary>Username/email not found, or password wrong (generic, per BR-03). HTTP 401.</summary>
    public const string InvalidCredentials = "AUTH_INVALID_CREDENTIALS";

    /// <summary>Too many failed attempts (BR-03). HTTP 429.</summary>
    public const string AccountLocked = "AUTH_ACCOUNT_LOCKED";

    /// <summary>Account deactivated by an Admin (UC-14). HTTP 403.</summary>
    public const string AccountDeactivated = "AUTH_ACCOUNT_DEACTIVATED";

    /// <summary>Teacher application awaiting Admin review. HTTP 403.</summary>
    public const string ApplicationPending = "AUTH_APPLICATION_PENDING";

    /// <summary>Teacher application rejected; returns review_comments. HTTP 403.</summary>
    public const string ApplicationRejected = "AUTH_APPLICATION_REJECTED";

    /// <summary>Registration or password-reset token expired. HTTP 410.</summary>
    public const string TokenExpired = "AUTH_TOKEN_EXPIRED";

    /// <summary>Token malformed, already used, or revoked. HTTP 401.</summary>
    public const string TokenInvalid = "AUTH_TOKEN_INVALID";

    /// <summary>Confirmation attempted, but the email now belongs to a confirmed account. HTTP 409.</summary>
    public const string EmailAlreadyConfirmed = "AUTH_EMAIL_ALREADY_CONFIRMED";

    /// <summary>Teacher certificate rejected (wrong type or too large, BR-05). HTTP 400.</summary>
    public const string CertificateInvalid = "AUTH_CERTIFICATE_INVALID";

    /// <summary>Google OAuth failed: code exchange error, or the Google email is unverified (UC-07).</summary>
    public const string GoogleAuthFailed = "AUTH_GOOGLE_FAILED";

    /// <summary>Change password: currentPassword does not match the stored hash (UC-03). HTTP 400.</summary>
    public const string InvalidCurrentPassword = "AUTH_INVALID_CURRENT_PASSWORD";

    /// <summary>Change password: newPassword is identical to the current one (UC-03). HTTP 400.</summary>
    public const string SamePassword = "AUTH_SAME_PASSWORD";

    /// <summary>Change password: the account has no local password (Google-only sign-in). HTTP 400.</summary>
    public const string NoPasswordSet = "AUTH_NO_PASSWORD_SET";
}
