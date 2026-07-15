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

    private static string GetRefreshTokenKey(string refreshToken)
        => $"refresh:{refreshToken}";

    private static string GetRefreshSessionSetKey(string accountId)
        => $"session:refresh:{accountId}";

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

    public async Task StoreRefreshSessionAsync(
        string accountId,
        string refreshToken,
        string accessTokenJti,
        DateTime accessTokenExpiresAtUtc,
        TimeSpan refreshTokenTtl)
    {
        var refreshTokenKey = GetRefreshTokenKey(refreshToken);
        var sessionSetKey = GetRefreshSessionSetKey(accountId);

        var accessExpiryUnix = new DateTimeOffset(accessTokenExpiresAtUtc, TimeSpan.Zero).ToUnixTimeSeconds();

        // Value carries everything needed to revoke the paired access token later.
        await _database.StringSetAsync(
            refreshTokenKey,
            $"{accountId}|{accessTokenJti}|{accessExpiryUnix}",
            refreshTokenTtl);

        await _database.SetAddAsync(sessionSetKey, refreshToken);
        await _database.KeyExpireAsync(sessionSetKey, refreshTokenTtl);
    }

    public async Task<string?> GetAccountIdByRefreshTokenAsync(string refreshToken)
    {
        var value = await _database.StringGetAsync(GetRefreshTokenKey(refreshToken));

        if (!value.HasValue)
        {
            return null;
        }

        return ParseAccountId(value.ToString());
    }

    public async Task RemoveRefreshSessionAsync(string accountId, string refreshToken)
    {
        await _database.KeyDeleteAsync(GetRefreshTokenKey(refreshToken));
        await _database.SetRemoveAsync(GetRefreshSessionSetKey(accountId), refreshToken);
    }

    public async Task RevokeAllSessionsAsync(string accountId)
    {
        var sessionSetKey = GetRefreshSessionSetKey(accountId);
        var refreshTokens = await _database.SetMembersAsync(sessionSetKey);

        foreach (var refreshToken in refreshTokens)
        {
            var refreshTokenKey = GetRefreshTokenKey(refreshToken.ToString());
            var value = await _database.StringGetAsync(refreshTokenKey);

            if (value.HasValue && TryParseSession(value.ToString(), out var jti, out var accessExpiresAtUtc))
            {
                await BlacklistTokenAsync(jti, accessExpiresAtUtc - DateTime.UtcNow);
            }

            await _database.KeyDeleteAsync(refreshTokenKey);
        }

        await _database.KeyDeleteAsync(sessionSetKey);
    }

    private static string ParseAccountId(string value)
    {
        var separatorIndex = value.IndexOf('|');
        return separatorIndex < 0 ? value : value[..separatorIndex];
    }

    private static bool TryParseSession(string value, out string jti, out DateTime accessExpiresAtUtc)
    {
        jti = string.Empty;
        accessExpiresAtUtc = DateTime.MinValue;

        var parts = value.Split('|');
        if (parts.Length != 3 || !long.TryParse(parts[2], out var expiryUnix))
        {
            return false;
        }

        jti = parts[1];
        accessExpiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(expiryUnix).UtcDateTime;
        return true;
    }
}

