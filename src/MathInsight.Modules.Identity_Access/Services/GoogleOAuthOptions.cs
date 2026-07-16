namespace MathInsight.Modules.Identity_Access.Services;

/// <summary>
/// Google OAuth 2.0 client configuration (UC-07, Flow A). Bound from the <c>GoogleOAuth</c>
/// configuration section; the secret values are supplied via environment variables / user secrets
/// and are never committed.
/// </summary>
public class GoogleOAuthOptions
{
    public const string SectionName = "GoogleOAuth";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Must match the redirect URI registered in the Google Console.</summary>
    public string RedirectUri { get; set; } = string.Empty;
}
