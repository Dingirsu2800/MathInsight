namespace MathInsight.Shared.Events;

public sealed record BlueprintReviewedEvent(
    string BlueprintId,
    string OwnerExpertId,
    string Status,
    string? ReviewNote) : MediatR.INotification;
