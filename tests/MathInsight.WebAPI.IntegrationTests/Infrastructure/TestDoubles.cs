using System.Collections.Concurrent;
using MathInsight.Modules.Identity_Access.Services;
using MathInsight.Shared.Storage;

namespace MathInsight.WebAPI.IntegrationTests.Infrastructure;

/// <summary>
/// Captures the emails the pipeline would have sent, so a test can read back the confirmation or
/// reset token the handler generated and drive the second half of a two-step flow through the API
/// exactly as a real user would.
/// </summary>
public sealed class CapturingEmailService : IEmailService
{
    public sealed record SentEmail(string Kind, string Recipient, string Token);

    private readonly ConcurrentQueue<SentEmail> _sent = new();

    public IReadOnlyCollection<SentEmail> Sent => _sent.ToArray();

    public string? LastTokenFor(string kind, string recipient) => _sent
        .Where(mail => mail.Kind == kind && string.Equals(mail.Recipient, recipient, StringComparison.OrdinalIgnoreCase))
        .Select(mail => mail.Token)
        .LastOrDefault();

    public void Clear() => _sent.Clear();

    public Task SendRegistrationConfirmationAsync(string email, string confirmationToken, CancellationToken cancellationToken)
    {
        _sent.Enqueue(new SentEmail("confirmation", email, confirmationToken));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string email, string resetToken, CancellationToken cancellationToken)
    {
        _sent.Enqueue(new SentEmail("reset", email, resetToken));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Stands in for Google's OAuth endpoints. Tests set <see cref="NextProfile"/> to model a verified
/// sign-in, an unverified email, or a failed code exchange (null) — no network call is made.
/// </summary>
public sealed class FakeGoogleOAuthService : IGoogleOAuthService
{
    public GoogleUserProfile? NextProfile { get; set; }

    public string BuildAuthorizationUrl(string state) =>
        $"https://accounts.google.test/o/oauth2/v2/auth?state={state}";

    public Task<GoogleUserProfile?> ExchangeCodeForProfileAsync(string code, CancellationToken cancellationToken) =>
        Task.FromResult(NextProfile);
}

/// <summary>
/// Records every <see cref="MathInsight.Shared.Events.StreakReminderEvent"/> the real MediatR
/// pipeline publishes, so a system test can assert who was reminded. Registered ALONGSIDE the
/// Notification module's own handler — MediatR notification handlers are additive, so this observes
/// the genuine publication rather than replacing it.
/// </summary>
public sealed class StreakReminderRecorder : MediatR.INotificationHandler<MathInsight.Shared.Events.StreakReminderEvent>
{
    private readonly ConcurrentQueue<MathInsight.Shared.Events.StreakReminderEvent> _received = new();

    public IReadOnlyCollection<MathInsight.Shared.Events.StreakReminderEvent> Received => _received.ToArray();

    public void Clear() => _received.Clear();

    public Task Handle(MathInsight.Shared.Events.StreakReminderEvent notification, CancellationToken cancellationToken)
    {
        _received.Enqueue(notification);
        return Task.CompletedTask;
    }
}

/// <summary>Returns a deterministic URL instead of uploading teacher certificates to Cloudinary.</summary>
public sealed class FakeImageStorage : IImageStorage
{
    public int UploadCount { get; private set; }

    public Task<string> UploadAsync(ImageUploadRequest request, CancellationToken cancellationToken)
    {
        UploadCount++;
        return Task.FromResult($"https://cdn.test/{request.Folder}/{UploadCount}-{request.FileName}");
    }
}
