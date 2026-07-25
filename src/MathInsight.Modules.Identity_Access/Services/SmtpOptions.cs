namespace MathInsight.Modules.Identity_Access.Services;

/// <summary>
/// SMTP settings bound from the <c>Smtp</c> configuration section. Credentials are never
/// committed — <c>appsettings.json</c> holds empty placeholders; real values come from user
/// secrets or environment variables.
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    /// <summary>When false (or when <see cref="Host"/> is empty), the logging fallback is used.</summary>
    public bool Enabled { get; set; }

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;
}
