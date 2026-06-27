using StackExchange.Redis;

namespace MathInsight.Modules.Identity_Access.Services.Auth;

public class RedisAuthSessionService : IAuthSessionService
{
    private readonly IDatabase _database;

    public RedisAuthSessionService(IConnectionMultiplexer redis)
    {
        _database = redis.GetDatabase();
    }

    public async Task<bool> IsLockedAsync(string accountId)
    {
        var lockedKey = GetLockedKey(accountId);
        return await _database.KeyExistsAsync(lockedKey);
    }

    public async Task RecordFailedLoginAsync(string accountId)
    {
        var failedKey = GetFailedKey(accountId);
        var lockedKey = GetLockedKey(accountId);

        var failedCount = await _database.StringIncrementAsync(failedKey);

        if(failedCount == 1)
            await _database.KeyExpireAsync(failedKey, TimeSpan.FromMinutes(10));

        if (failedCount >= 5)
            await _database.StringSetAsync(
                lockedKey,
                "locked",
                TimeSpan.FromMinutes(15));
    }

    public async Task ResetFailedLoginAsync(string accountId)
    {
        await _database.KeyDeleteAsync(GetFailedKey(accountId));
        await _database.KeyDeleteAsync(GetLockedKey(accountId));
    }

    public async Task StoreActiveSessionAsync(string accountId, string tokenId, TimeSpan ttl)
    {
        var activeSessionKey = GetActiveSessionKey(accountId);

        var oldTokenId = await _database.StringGetAsync(activeSessionKey);
        var oldTtl = await _database.KeyTimeToLiveAsync(activeSessionKey);

        if (oldTokenId.HasValue &&
            oldTokenId.ToString() != tokenId &&
            oldTtl.HasValue &&
            oldTtl.Value > TimeSpan.Zero)
        {
            await BlacklistTokenAsync(oldTokenId.ToString(), oldTtl.Value);
        }

        await _database.StringSetAsync(activeSessionKey, tokenId, ttl);
    }

    public async Task<bool> IsActiveSessionAsync(string accountId, string tokenId)
    {
        var activeSessionKey = GetActiveSessionKey(accountId);
        var storedToken = await _database.StringGetAsync(activeSessionKey);

        return storedToken.HasValue && storedToken == tokenId;
    }

    private static string GetFailedKey(string accountId)
        => $"login:fail:{accountId}";

    private static string GetLockedKey(string accountId)
        => $"login:locked:{accountId}";

    private static string GetActiveSessionKey(string accountId)
        => $"auth:active-session:{accountId}";

    private static string GetBlacklistedTokenKey(string tokenId)
    => $"jwt:blacklist:{tokenId}";

    public async Task BlacklistTokenAsync(string tokenId, TimeSpan ttl)
    {
        if (ttl <= TimeSpan.Zero)
        {
            return;
        }

        await _database.StringSetAsync(
            GetBlacklistedTokenKey(tokenId),
            "blacklisted",
            ttl);
    }

    public async Task<bool> IsTokenBlacklistedAsync(string tokenId)
    {
        return await _database.KeyExistsAsync(GetBlacklistedTokenKey(tokenId));
    }
}

