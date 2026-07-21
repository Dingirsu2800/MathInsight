namespace MathInsight.Modules.Gamification.Services;

/// <summary>
/// BR-45. Detects students with no qualifying activity yet today and publishes a
/// StreakReminderEvent for each (consumed later by the Notification module). Holds the reminder
/// business logic independently of the timer, so it is unit-testable without the BackgroundService.
/// </summary>
public interface IStreakReminderService
{
    /// <summary>
    /// Publishes a reminder for every student whose streak has not been advanced on
    /// <paramref name="today"/>. Returns the number of reminders published.
    /// </summary>
    Task<int> SendRemindersAsync(DateOnly today, CancellationToken cancellationToken = default);
}
