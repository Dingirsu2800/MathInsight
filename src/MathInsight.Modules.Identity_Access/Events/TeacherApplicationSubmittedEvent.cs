using MediatR;

namespace MathInsight.Modules.Identity_Access.Events;

/// <summary>
/// Raised at email confirmation of a Teacher, when the <c>TeacherApplication</c> row comes into
/// existence (not at registration). Consumed by the Notification module to alert Admins of a new
/// application to review.
/// </summary>
public record TeacherApplicationSubmittedEvent : INotification
{
    public string ApplicationId { get; init; } = default!;
    public string TeacherId { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string DocumentsUrl { get; init; } = default!;
}
