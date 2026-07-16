namespace MathInsight.Modules.Identity_Access.Services;

/// <summary>
/// Stores password-reset tokens in Redis under <c>password:reset:{token}</c> (UC-06, BR-14).
/// Mirrors <see cref="IPendingRegistrationStore"/>: a short-lived, single-use token that maps
/// to the account it was issued for. The value is the account id only — no credential material.
/// </summary>
public interface IPasswordResetTokenStore
{
    /// <summary>
    /// Generates a reset token, persists <c>password:reset:{token}</c> → accountId with a
    /// 15-minute TTL (BR-14), and returns the freshly generated token.
    /// </summary>
    Task<string> CreateAsync(string accountId, CancellationToken cancellationToken);

    /// <summary>Resolves the account a reset token belongs to, or null if it is missing/expired.</summary>
    Task<string?> GetAccountIdAsync(string token, CancellationToken cancellationToken);

    /// <summary>Deletes the token once it has been consumed, so it cannot be replayed.</summary>
    Task DeleteAsync(string token, CancellationToken cancellationToken);
}
