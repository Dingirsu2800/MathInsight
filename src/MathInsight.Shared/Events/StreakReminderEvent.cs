using MediatR;

namespace MathInsight.Shared.Events;

/// <summary>
/// Raised for a student who has no qualifying activity yet today and therefore risks losing their
/// study streak (BR-45). Consumed by the Notification module (008) to push the reminder; the
/// Gamification module only detects inactivity and publishes this event.
/// </summary>
public record StreakReminderEvent(
    string StudentId,
    int CurrentStreak,
    DateOnly? LastActivityDate
) : INotification;
