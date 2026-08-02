using System.Text.Json;
using MathInsight.Modules.Notification_Report.Contracts;
using StackExchange.Redis;

namespace MathInsight.Modules.Notification_Report.Services;

/// <summary>BR-19: Redis-backed leaderboard cache, key "ntf:leaderboard:{grade}", TTL 25h (covers
/// one day plus slack for the daily recalculation job to catch up if it runs late).</summary>
public class RedisLeaderboardCacheService : ILeaderboardCacheService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(25);

    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public RedisLeaderboardCacheService(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    public async Task<IReadOnlyList<LeaderboardEntryDto>?> GetAsync(int grade, CancellationToken cancellationToken = default)
    {
        var db = _connectionMultiplexer.GetDatabase();
        var value = await db.StringGetAsync(Key(grade));

        if (value.IsNullOrEmpty)
        {
            return null;
        }

        return JsonSerializer.Deserialize<List<LeaderboardEntryDto>>((string)value!);
    }

    public async Task SetAsync(int grade, IReadOnlyList<LeaderboardEntryDto> entries, CancellationToken cancellationToken = default)
    {
        var db = _connectionMultiplexer.GetDatabase();
        var json = JsonSerializer.Serialize(entries);
        await db.StringSetAsync(Key(grade), json, Ttl);
    }

    private static string Key(int grade) => $"ntf:leaderboard:{grade}";
}
