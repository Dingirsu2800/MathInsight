namespace MathInsight.Modules.Identity_Access.Services;

/// <summary>
/// Stores the short-lived, single-use CSRF <c>state</c> value for the Google OAuth flow (UC-07)
/// under <c>oauth:state:{state}</c>. Mirrors the other transient stores: Redis-backed in
/// production, with an in-memory fallback for local development when Redis is disabled.
/// </summary>
public interface IOAuthStateStore
{
    /// <summary>Generates a state value, persists it with a short TTL, and returns it.</summary>
    Task<string> CreateAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Verifies and consumes a state value. Returns true only if it existed and had not expired;
    /// the value is deleted so it can never be replayed (single-use).
    /// </summary>
    Task<bool> ConsumeAsync(string state, CancellationToken cancellationToken);
}
