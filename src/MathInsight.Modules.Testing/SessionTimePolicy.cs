namespace MathInsight.Modules.Testing;

public static class SessionTimePolicy
{
    public static bool HasTimeLimit(int durationMinutes) => durationMinutes > 0;
    public static bool IsExpired(DateTime startTime, int durationMinutes, DateTime now) => HasTimeLimit(durationMinutes) && now >= startTime.AddMinutes(durationMinutes);
    public static int? RemainingSeconds(DateTime startTime, int durationMinutes, DateTime now) => HasTimeLimit(durationMinutes) ? Math.Max(0, (int)Math.Ceiling(startTime.AddMinutes(durationMinutes).Subtract(now).TotalSeconds)) : null;
    public static int ElapsedSeconds(DateTime startTime, DateTime now) => Math.Max(0, (int)Math.Floor(now.Subtract(startTime).TotalSeconds));
}
