using MediatR;

namespace MathInsight.Shared.Events;

/// <summary>
/// Event published when a student meets the condition for a badge and the badge is awarded.
/// Consumed by Gamification/Notification modules.
/// </summary>
public record BadgeAwardedEvent(
    string StudentId,
    string BadgeId,
    string BadgeName,
    DateTime AwardedTime) : INotification;
