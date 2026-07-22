namespace MathInsight.Modules.Gamification.Queries.GetStreak;

/// <summary>
/// UC-81 read model. <paramref name="CurrentStreak"/> is the value to DISPLAY: it is the stored
/// counter while the streak is still alive, or 0 once it has lapsed (see the handler). IsActive
/// drives the 🔥 vs "Streak Broken" state; isBroken is simply its inverse.
/// <paramref name="LongestStreak"/> is always the stored all-time best.
/// </summary>
public sealed record StreakResponse(
    int CurrentStreak,
    int LongestStreak,
    DateOnly? LastActivityDate,
    bool IsActive);
