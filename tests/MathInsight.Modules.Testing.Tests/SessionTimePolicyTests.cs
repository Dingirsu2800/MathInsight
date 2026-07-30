using MathInsight.Modules.Testing;

namespace MathInsight.Modules.Testing.Tests;

public sealed class SessionTimePolicyTests
{
    [Fact]
    public void UnlimitedDuration_HasNoDeadlineOrRemainingSeconds()
    {
        var start = new DateTime(2026, 7, 30, 10, 0, 0, DateTimeKind.Utc);
        var now = start.AddHours(3);
        Assert.False(SessionTimePolicy.HasTimeLimit(0));
        Assert.False(SessionTimePolicy.IsExpired(start, 0, now));
        Assert.Null(SessionTimePolicy.RemainingSeconds(start, 0, now));
        Assert.Equal(10800, SessionTimePolicy.ElapsedSeconds(start, now));
    }
}
