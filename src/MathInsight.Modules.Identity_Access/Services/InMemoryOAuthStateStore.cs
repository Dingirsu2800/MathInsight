using System.Collections.Concurrent;

namespace MathInsight.Modules.Identity_Access.Services;

/// <summary>
/// In-memory fallback for <see cref="IOAuthStateStore"/>, used when Redis is disabled
/// (<c>Redis:Enabled = false</c>, local development only). State values are held in process memory
/// and lost on restart.
/// </summary>
public class InMemoryOAuthStateStore : IOAuthStateStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, DateTime> _states = new();

    public Task<string> CreateAsync(CancellationToken cancellationToken)
    {
        var state = Guid.NewGuid().ToString("N");
        _states[state] = DateTime.UtcNow.Add(Ttl);
        return Task.FromResult(state);
    }

    public Task<bool> ConsumeAsync(string state, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(state) || !_states.TryRemove(state, out var expiresAtUtc))
        {
            return Task.FromResult(false);
        }

        // Single-use: already removed above; only valid if it had not expired.
        return Task.FromResult(expiresAtUtc > DateTime.UtcNow);
    }
}
