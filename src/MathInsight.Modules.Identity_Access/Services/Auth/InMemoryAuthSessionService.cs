using System.Collections.Concurrent;

namespace MathInsight.Modules.Identity_Access.Services.Auth;

public class InMemoryAuthSessionService : IAuthSessionService
{
    private readonly ConcurrentDictionary<string, FailedLoginState> _failedLogins = new();
    private readonly ConcurrentDictionary<string, DateTime> _lockedAccounts = new();
    private readonly ConcurrentDictionary<string, DateTime> _blacklistedTokens = new();

    // refreshToken -> its session record; and accountId -> set of its refresh tokens.
    private readonly ConcurrentDictionary<string, RefreshSessionState> _refreshSessions = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _accountRefreshTokens = new();

    public Task<bool> IsLockedAsync(string accountId)
    {
        if (!_lockedAccounts.TryGetValue(accountId, out var lockedUntilUtc))
        {
            return Task.FromResult(false);
        }

        if (lockedUntilUtc > DateTime.UtcNow)
        {
            return Task.FromResult(true);
        }

        _lockedAccounts.TryRemove(accountId, out _);
        return Task.FromResult(false);
    }

    public Task RecordFailedLoginAsync(string accountId)
    {
        var now = DateTime.UtcNow;

        var state = _failedLogins.AddOrUpdate(
            accountId,
            _ => new FailedLoginState(1, now.AddMinutes(10)),
            (_, current) =>
            {
                if (current.ExpiresAtUtc <= now)
                {
                    return new FailedLoginState(1, now.AddMinutes(10));
                }

                return current with { Count = current.Count + 1 };
            });

        if (state.Count >= 5)
        {
            _lockedAccounts[accountId] = now.AddMinutes(15);
        }

        return Task.CompletedTask;
    }

    public Task ResetFailedLoginAsync(string accountId)
    {
        _failedLogins.TryRemove(accountId, out _);
        _lockedAccounts.TryRemove(accountId, out _);
        return Task.CompletedTask;
    }

    public Task BlacklistTokenAsync(string tokenId, TimeSpan ttl)
    {
        if (ttl <= TimeSpan.Zero)
        {
            return Task.CompletedTask;
        }

        _blacklistedTokens[tokenId] = DateTime.UtcNow.Add(ttl);
        return Task.CompletedTask;
    }

    public Task<bool> IsTokenBlacklistedAsync(string tokenId)
    {
        if (!_blacklistedTokens.TryGetValue(tokenId, out var expiresAtUtc))
        {
            return Task.FromResult(false);
        }

        if (expiresAtUtc > DateTime.UtcNow)
        {
            return Task.FromResult(true);
        }

        _blacklistedTokens.TryRemove(tokenId, out _);
        return Task.FromResult(false);
    }

    public Task StoreRefreshSessionAsync(
        string accountId,
        string refreshToken,
        string accessTokenJti,
        DateTime accessTokenExpiresAtUtc,
        TimeSpan refreshTokenTtl)
    {
        _refreshSessions[refreshToken] = new RefreshSessionState(
            accountId,
            accessTokenJti,
            accessTokenExpiresAtUtc,
            DateTime.UtcNow.Add(refreshTokenTtl));

        var tokens = _accountRefreshTokens.GetOrAdd(accountId, _ => new ConcurrentDictionary<string, byte>());
        tokens[refreshToken] = 0;

        return Task.CompletedTask;
    }

    public Task<string?> GetAccountIdByRefreshTokenAsync(string refreshToken)
    {
        if (!_refreshSessions.TryGetValue(refreshToken, out var session))
        {
            return Task.FromResult<string?>(null);
        }

        if (session.RefreshExpiresAtUtc <= DateTime.UtcNow)
        {
            RemoveRefreshToken(session.AccountId, refreshToken);
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult<string?>(session.AccountId);
    }

    public Task RemoveRefreshSessionAsync(string accountId, string refreshToken)
    {
        RemoveRefreshToken(accountId, refreshToken);
        return Task.CompletedTask;
    }

    public Task RevokeAllSessionsAsync(string accountId)
    {
        if (!_accountRefreshTokens.TryRemove(accountId, out var tokens))
        {
            return Task.CompletedTask;
        }

        foreach (var refreshToken in tokens.Keys)
        {
            if (_refreshSessions.TryRemove(refreshToken, out var session) &&
                session.AccessTokenExpiresAtUtc > DateTime.UtcNow)
            {
                _blacklistedTokens[session.AccessTokenJti] = session.AccessTokenExpiresAtUtc;
            }
        }

        return Task.CompletedTask;
    }

    private void RemoveRefreshToken(string accountId, string refreshToken)
    {
        _refreshSessions.TryRemove(refreshToken, out _);

        if (_accountRefreshTokens.TryGetValue(accountId, out var tokens))
        {
            tokens.TryRemove(refreshToken, out _);
        }
    }

    private sealed record FailedLoginState(int Count, DateTime ExpiresAtUtc);

    private sealed record RefreshSessionState(
        string AccountId,
        string AccessTokenJti,
        DateTime AccessTokenExpiresAtUtc,
        DateTime RefreshExpiresAtUtc);
}
