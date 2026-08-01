using MathInsight.Modules.Notification_Report.Contracts;

namespace MathInsight.Modules.Notification_Report.Services;

/// <summary>Used when Redis:Enabled is false — every read is a miss, every write is a no-op.</summary>
public class NullLeaderboardCacheService : ILeaderboardCacheService
{
    public Task<IReadOnlyList<LeaderboardEntryDto>?> GetAsync(int grade, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<LeaderboardEntryDto>?>(null);

    public Task SetAsync(int grade, IReadOnlyList<LeaderboardEntryDto> entries, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
