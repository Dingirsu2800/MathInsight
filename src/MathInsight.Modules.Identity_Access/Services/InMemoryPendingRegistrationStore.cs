using System.Collections.Concurrent;

namespace MathInsight.Modules.Identity_Access.Services;

/// <summary>
/// In-memory fallback for <see cref="IPendingRegistrationStore"/>, used when Redis is disabled
/// (<c>Redis:Enabled = false</c>, local development only). Mirrors the pattern of
/// <c>InMemoryAuthSessionService</c>. Pending registrations are held in process memory and are
/// <b>lost on restart</b> — see the Known Limitations section of the spec.
/// </summary>
public class InMemoryPendingRegistrationStore : IPendingRegistrationStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private readonly ConcurrentDictionary<string, Entry> _pending = new();

    public Task<string> SaveAsync(PendingRegistration payload, CancellationToken cancellationToken)
    {
        var token = Guid.NewGuid().ToString("N");
        _pending[token] = new Entry(payload, DateTime.UtcNow.Add(Ttl));
        return Task.FromResult(token);
    }

    public Task<PendingRegistration?> GetAsync(string token, CancellationToken cancellationToken)
    {
        if (!_pending.TryGetValue(token, out var entry))
        {
            return Task.FromResult<PendingRegistration?>(null);
        }

        if (entry.ExpiresAtUtc <= DateTime.UtcNow)
        {
            _pending.TryRemove(token, out _);
            return Task.FromResult<PendingRegistration?>(null);
        }

        return Task.FromResult<PendingRegistration?>(entry.Payload);
    }

    public Task DeleteAsync(string token, CancellationToken cancellationToken)
    {
        _pending.TryRemove(token, out _);
        return Task.CompletedTask;
    }

    private sealed record Entry(PendingRegistration Payload, DateTime ExpiresAtUtc);
}
