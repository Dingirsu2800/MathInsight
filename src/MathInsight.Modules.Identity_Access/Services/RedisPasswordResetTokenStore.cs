using StackExchange.Redis;

namespace MathInsight.Modules.Identity_Access.Services;

/// <summary>
/// Redis-backed implementation of <see cref="IPasswordResetTokenStore"/>. Follows the same
/// pattern as <see cref="RedisPendingRegistrationStore"/>: Redis is the sole store for the
/// transient token (UC-06, BR-14, 15-minute TTL).
/// </summary>
public class RedisPasswordResetTokenStore : IPasswordResetTokenStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);

    private readonly IDatabase _database;

    public RedisPasswordResetTokenStore(IConnectionMultiplexer redis)
    {
        _database = redis.GetDatabase();
    }

    public async Task<string> CreateAsync(string accountId, CancellationToken cancellationToken)
    {
        var token = Guid.NewGuid().ToString("N");

        await _database.StringSetAsync(GetKey(token), accountId, Ttl);

        return token;
    }

    public async Task<string?> GetAccountIdAsync(string token, CancellationToken cancellationToken)
    {
        var value = await _database.StringGetAsync(GetKey(token));

        return value.HasValue ? value.ToString() : null;
    }

    public async Task DeleteAsync(string token, CancellationToken cancellationToken)
    {
        await _database.KeyDeleteAsync(GetKey(token));
    }

    private static string GetKey(string token) => $"password:reset:{token}";
}
