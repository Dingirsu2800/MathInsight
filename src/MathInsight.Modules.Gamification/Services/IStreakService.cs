using MathInsight.Modules.Gamification.Enums;

namespace MathInsight.Modules.Gamification.Services;

/// <summary>
/// Study-streak maintenance (BR-39..BR-42). The BR-39 qualification decision lives inside the
/// service: callers pass the raw activity type and duration, and the service decides whether the
/// activity advances the streak — so no caller duplicates the qualification rule.
/// </summary>
public interface IStreakService
{
    /// <summary>
    /// Applies one activity to the student's streak. A non-qualifying activity is a no-op
    /// (no row created, no update). Same-day repeats are idempotent.
    /// </summary>
    /// <param name="studentId">Owning student (StudyStreak is unique per student).</param>
    /// <param name="activityType">Raw activity type; qualification is decided here (BR-39).</param>
    /// <param name="durationSeconds">Activity duration; only relevant to VIEW_LECTURE (>= 300).</param>
    /// <param name="activityDate">Calendar date of the activity (streaks are day-based).</param>
    Task UpdateStreakAsync(
        string studentId,
        ActivityType activityType,
        int durationSeconds,
        DateOnly activityDate,
        CancellationToken cancellationToken = default);
}
