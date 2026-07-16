namespace MathInsight.Modules.Identity_Access.Services;

/// <summary>
/// Stores self-registration payloads in Redis until email confirmation (BR-04). This is the
/// only place a pending registration lives — nothing is written to SQL before confirmation.
/// </summary>
public interface IPendingRegistrationStore
{
    /// <summary>
    /// Persists the payload under <c>pending:register:{token}</c> with a 24-hour TTL (BR-14)
    /// and returns the freshly generated confirmation token.
    /// </summary>
    Task<string> SaveAsync(PendingRegistration payload, CancellationToken cancellationToken);

    /// <summary>Reads the payload for a confirmation token, or null if it is missing/expired.</summary>
    Task<PendingRegistration?> GetAsync(string token, CancellationToken cancellationToken);

    /// <summary>Deletes the payload once it has been consumed at confirmation.</summary>
    Task DeleteAsync(string token, CancellationToken cancellationToken);
}
