namespace MathInsight.Modules.Identity_Access.Services.Auth;

public interface IAuthSessionService
{
    Task<bool> IsLockedAsync(string accountId);
    Task RecordFailedLoginAsync(string accountId);
    Task ResetFailedLoginAsync(string accountId);

    Task BlacklistTokenAsync(string tokenId, TimeSpan ttl);
    Task<bool> IsTokenBlacklistedAsync(string tokenId);

    // --- Refresh-token session model (DD-02) ---

    /// <summary>
    /// Records a refresh token for an account: maps <c>refresh:{token}</c> → account and adds the
    /// token to the account's <c>session:refresh:{accountId}</c> set. The associated access-token
    /// jti and its expiry are kept so the token can be blacklisted when the session is revoked.
    /// </summary>
    Task StoreRefreshSessionAsync(
        string accountId,
        string refreshToken,
        string accessTokenJti,
        DateTime accessTokenExpiresAtUtc,
        TimeSpan refreshTokenTtl);

    /// <summary>Resolves the account a refresh token belongs to, or null if missing/expired/rotated.</summary>
    Task<string?> GetAccountIdByRefreshTokenAsync(string refreshToken);

    /// <summary>Deletes a single refresh token from an account's session set (logout / rotation).</summary>
    Task RemoveRefreshSessionAsync(string accountId, string refreshToken);

    /// <summary>
    /// Revokes every session for an account (BR-15): deletes all of its refresh tokens and
    /// blacklists their outstanding access-token jtis. Also used to enforce Student single-session
    /// (BR-02) — call this before issuing the new session's tokens.
    /// </summary>
    Task RevokeAllSessionsAsync(string accountId);
}
