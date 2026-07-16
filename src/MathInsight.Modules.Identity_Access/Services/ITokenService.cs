using MathInsight.Modules.Identity_Access.Entities;

namespace MathInsight.Modules.Identity_Access.Services;

public interface ITokenService
{
    /// <summary>Creates a short-lived (15 min) access token (JWT) with the DD-02 claims.</summary>
    string CreateAccessToken(Account account, out DateTime expiresAt, out string tokenId);

    /// <summary>
    /// Issues an opaque refresh token (GUID, 7 days) bound to the account and to the access token
    /// it was minted alongside, then persists it via the session service (DD-02).
    /// </summary>
    Task<string> IssueRefreshTokenAsync(
        string accountId,
        string accessTokenJti,
        DateTime accessTokenExpiresAtUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Rotates a refresh token: validates and deletes the current one so it can never be reused,
    /// then issues a fresh refresh token bound to the new access token. Returns null if the
    /// current token is missing, expired, or already rotated (UC-95).
    /// </summary>
    Task<RefreshTokenResult?> RotateRefreshTokenAsync(
        string currentRefreshToken,
        string newAccessTokenJti,
        DateTime newAccessTokenExpiresAtUtc,
        CancellationToken cancellationToken);

    /// <summary>Blacklists an access token's jti for its remaining lifetime (BR-10).</summary>
    Task BlacklistAccessTokenAsync(string accessTokenJti, TimeSpan remainingLifetime);
}

/// <summary>Result of a successful refresh-token rotation.</summary>
public sealed record RefreshTokenResult(string AccountId, string RefreshToken);
