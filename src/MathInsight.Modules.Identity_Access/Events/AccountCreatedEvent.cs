using MediatR;

namespace MathInsight.Modules.Identity_Access.Events;

/// <summary>
/// Raised when an <c>Account</c> row is created (at email confirmation, manual create, import, or
/// OAuth). Consumed by the Notification module (welcome email). Published as a MediatR
/// <see cref="INotification"/>, matching the repo's cross-module event convention.
/// </summary>
public record AccountCreatedEvent : INotification
{
    public string AccountId { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string Username { get; init; } = default!;
    public string RoleName { get; init; } = default!;
    public string FirstName { get; init; } = default!;
    public string LastName { get; init; } = default!;
}
