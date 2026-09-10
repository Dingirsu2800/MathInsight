namespace MathInsight.Shared.Events;

/// <summary>Post-commit request to persist a role-specific inbox notification.</summary>
public sealed record NotificationRequestedEvent(
    string AccountId,
    string Title,
    string Content,
    string? Link,
    string DeduplicationKey) : MediatR.INotification;
