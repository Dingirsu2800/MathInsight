namespace MathInsight.Modules.Identity_Access.Services.Auth;

public interface IAuthSessionService
{
    Task<bool> IsLockedAsync(string accountId);
    Task RecordFailedLoginAsync(string accountId);
    Task ResetFailedLoginAsync(string accountId);

    Task StoreActiveSessionAsync(string accountId, string tokenId, TimeSpan ttl);
    Task<bool> IsActiveSessionAsync(string accountId, string tokenId);

    Task BlacklistTokenAsync(string tokenId, TimeSpan ttl);
    Task<bool> IsTokenBlacklistedAsync(string tokenId);
}
