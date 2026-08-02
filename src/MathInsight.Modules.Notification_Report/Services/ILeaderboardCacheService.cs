using MathInsight.Modules.Notification_Report.Contracts;

namespace MathInsight.Modules.Notification_Report.Services;

/// <summary>BR-19: leaderboard cache abstraction, keyed by grade.</summary>
public interface ILeaderboardCacheService
{
    /// <summary>Returns null on a cache miss (or when caching is disabled) — callers fall back to a live query.</summary>
    Task<IReadOnlyList<LeaderboardEntryDto>?> GetAsync(int grade, CancellationToken cancellationToken = default);

    Task SetAsync(int grade, IReadOnlyList<LeaderboardEntryDto> entries, CancellationToken cancellationToken = default);
}
