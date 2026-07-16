namespace MathInsight.Modules.Identity_Access.Services;

/// <summary>
/// Talks to Google's OAuth 2.0 endpoints for the Authorization Code flow (UC-07, Flow A):
/// builds the consent URL and exchanges an authorization code for the user's verified profile.
/// </summary>
public interface IGoogleOAuthService
{
    /// <summary>
    /// Builds the Google authorization URL (scope: <c>openid email profile</c>) carrying the
    /// given CSRF <paramref name="state"/>. The browser is redirected here to grant consent.
    /// </summary>
    string BuildAuthorizationUrl(string state);

    /// <summary>
    /// Exchanges an authorization <paramref name="code"/> for tokens at Google's token endpoint,
    /// then fetches the user's profile from the userinfo endpoint. Returns null on any failure
    /// (bad code, network error, malformed response) so the caller can fail closed.
    /// </summary>
    Task<GoogleUserProfile?> ExchangeCodeForProfileAsync(string code, CancellationToken cancellationToken);
}

/// <summary>The subset of the Google profile this module needs (UC-07).</summary>
public sealed record GoogleUserProfile(
    string Sub,
    string Email,
    bool EmailVerified,
    string? FirstName,
    string? LastName);
