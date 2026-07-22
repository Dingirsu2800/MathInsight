using MathInsight.Modules.Identity_Access.Contracts;
using MathInsight.Shared.Results;

namespace MathInsight.Modules.Identity_Access.Errors;

/// <summary>
/// Auth failure outcomes as <see cref="Error"/> values (BR-13). The controller maps each code
/// to its HTTP status. Messages are developer-facing (BR-11); the frontend localizes by code.
/// </summary>
public static class AuthErrors
{
    public static readonly Error InvalidCredentials = new(
        AuthErrorCodes.InvalidCredentials,
        "Invalid username/email or password.");

    public static readonly Error AccountLocked = new(
        AuthErrorCodes.AccountLocked,
        "Account locked due to too many failed login attempts.");

    public static readonly Error AccountDeactivated = new(
        AuthErrorCodes.AccountDeactivated,
        "This account has been deactivated.");

    // UC-07: any Google OAuth failure (code exchange, unverified email). The controller redirects
    // to the frontend with ?error=google_failed; the specific code is not surfaced to the browser.
    public static readonly Error GoogleAuthFailed = new(
        AuthErrorCodes.GoogleAuthFailed,
        "Google authentication failed.");

    public static readonly Error ApplicationPending = new(
        AuthErrorCodes.ApplicationPending,
        "Teacher application is awaiting Admin review.");

    public static readonly Error TokenInvalid = new(
        AuthErrorCodes.TokenInvalid,
        "Refresh token is invalid, expired, or already used.");

    public static readonly Error TokenExpired = new(
        AuthErrorCodes.TokenExpired,
        "The confirmation token has expired or was already used.");

    // Reused at both registration time and confirmation time: the email/username already belongs
    // to a confirmed account (DD-01 — every persisted account is confirmed).
    public static readonly Error EmailAlreadyConfirmed = new(
        AuthErrorCodes.EmailAlreadyConfirmed,
        "An account with this email or username already exists.");

    /// <summary>
    /// Rejected teacher application (403). The admin's review comments are carried in the error
    /// message so the controller can surface them as <c>review_comments</c> (BR-13).
    /// </summary>
    public static Error ApplicationRejected(string? reviewComments) =>
        new(AuthErrorCodes.ApplicationRejected, reviewComments ?? string.Empty);

    // UC-03. Distinct from InvalidCredentials: the caller is already authenticated, so this is a
    // failed re-authentication of the current password, not a failed login. No account enumeration
    // risk — the account id came from the caller's own token.
    public static readonly Error InvalidCurrentPassword = new(
        AuthErrorCodes.InvalidCurrentPassword,
        "The current password is incorrect.");

    public static readonly Error SamePassword = new(
        AuthErrorCodes.SamePassword,
        "The new password must be different from the current password.");

    public static readonly Error NoPasswordSet = new(
        AuthErrorCodes.NoPasswordSet,
        "This account has no password to change; it signs in with Google.");

    /// <summary>Certificate failed BR-05 validation (400). Carries the validation detail.</summary>
    public static Error CertificateInvalid(string message) =>
        new(AuthErrorCodes.CertificateInvalid, message);
}
