namespace MathInsight.Modules.Identity_Access.Services;

/// <summary>
/// Sends transactional auth emails. Each email carries a clickable frontend link built from
/// <c>FrontendBaseUrl</c>, not a bare token.
/// </summary>
public interface IEmailService
{
    /// <summary>Sends the confirmation email with a link to {FrontendBaseUrl}/confirm-email?token=... (UC-93).</summary>
    Task SendRegistrationConfirmationAsync(string email, string confirmationToken, CancellationToken cancellationToken);

    /// <summary>
    /// Sends the password-reset email with a link to {FrontendBaseUrl}/reset-password?token=... (UC-06).
    /// Ready for the password-reset flow once it is implemented.
    /// </summary>
    Task SendPasswordResetAsync(string email, string resetToken, CancellationToken cancellationToken);
}
