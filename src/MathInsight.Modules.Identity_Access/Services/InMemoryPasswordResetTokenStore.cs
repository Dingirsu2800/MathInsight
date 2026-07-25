using System.Collections.Concurrent;

namespace MathInsight.Modules.Identity_Access.Services;

/// <summary>
/// In-memory fallback for <see cref="IPasswordResetTokenStore"/>, used when Redis is disabled
/// (<c>Redis:Enabled = false</c>, local development only). Mirrors
/// <see cref="InMemoryPendingRegistrationStore"/>: tokens are held in process memory and are
/// lost on restart.
/// </summary>
public class InMemoryPasswordResetTokenStore : IPasswordResetTokenStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, Entry> _tokens = new();

    public Task<string> CreateAsync(string accountId, CancellationToken cancellationToken)
    {
        var token = Guid.NewGuid().ToString("N");
        _tokens[token] = new Entry(accountId, DateTime.UtcNow.Add(Ttl));
        return Task.FromResult(token);
    }

    public Task<string?> GetAccountIdAsync(string token, CancellationToken cancellationToken)
    {
        if (!_tokens.TryGetValue(token, out var entry))
        {
            return Task.FromResult<string?>(null);
        }

        if (entry.ExpiresAtUtc <= DateTime.UtcNow)
        {
            _tokens.TryRemove(token, out _);
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult<string?>(entry.AccountId);
    }

    public Task DeleteAsync(string token, CancellationToken cancellationToken)
    {
        _tokens.TryRemove(token, out _);
        return Task.CompletedTask;
    }

    private sealed record Entry(string AccountId, DateTime ExpiresAtUtc);
}
