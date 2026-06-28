using System.Collections.Concurrent;

namespace MathInsight.Modules.Identity_Access.Services.Auth;

public class InMemoryAuthSessionService : IAuthSessionService
{
    private readonly ConcurrentDictionary<string, FailedLoginState> _failedLogins = new();
    private readonly ConcurrentDictionary<string, DateTime> _lockedAccounts = new();
    private readonly ConcurrentDictionary<string, SessionState> _activeSessions = new();
    private readonly ConcurrentDictionary<string, DateTime> _blacklistedTokens = new();

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

    public Task StoreActiveSessionAsync(string accountId, string tokenId, TimeSpan ttl)
    {
        var expiresAtUtc = DateTime.UtcNow.Add(ttl);

        if (_activeSessions.TryGetValue(accountId, out var oldSession) &&
            oldSession.TokenId != tokenId &&
            oldSession.ExpiresAtUtc > DateTime.UtcNow)
        {
            _blacklistedTokens[oldSession.TokenId] = oldSession.ExpiresAtUtc;
        }

        _activeSessions[accountId] = new SessionState(tokenId, expiresAtUtc);
        return Task.CompletedTask;
    }

    public Task<bool> IsActiveSessionAsync(string accountId, string tokenId)
    {
        if (!_activeSessions.TryGetValue(accountId, out var session))
        {
            return Task.FromResult(false);
        }

        if (session.ExpiresAtUtc <= DateTime.UtcNow)
        {
            _activeSessions.TryRemove(accountId, out _);
            return Task.FromResult(false);
        }

        return Task.FromResult(session.TokenId == tokenId);
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

    private sealed record FailedLoginState(int Count, DateTime ExpiresAtUtc);

    private sealed record SessionState(string TokenId, DateTime ExpiresAtUtc);
}
