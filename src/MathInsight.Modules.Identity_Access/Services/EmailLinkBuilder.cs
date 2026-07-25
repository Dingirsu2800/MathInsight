namespace MathInsight.Modules.Identity_Access.Services;

/// <summary>
/// Builds clickable frontend links for auth emails: {FrontendBaseUrl}/{path}?token={token}.
/// Shared by the SMTP and logging email services so the link format stays consistent.
/// </summary>
internal static class EmailLinkBuilder
{
    public static string ConfirmEmail(string frontendBaseUrl, string token) =>
        Build(frontendBaseUrl, "confirm-email", token);

    public static string ResetPassword(string frontendBaseUrl, string token) =>
        Build(frontendBaseUrl, "reset-password", token);

    private static string Build(string frontendBaseUrl, string path, string token) =>
        $"{frontendBaseUrl.TrimEnd('/')}/{path}?token={Uri.EscapeDataString(token)}";
}
