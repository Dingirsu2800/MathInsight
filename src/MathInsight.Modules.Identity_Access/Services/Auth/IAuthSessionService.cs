namespace MathInsight.Modules.Identity_Access.Services.Auth;

public interface IAuthSessionService
{
    Task<bool> IsLockedAsync(string accountId);
    Task RecordFailedLoginAsync(string accountId);
    Task ResetFailedLoginAsync(string accountId);
    Task StoreActiveSessionAsync(string accountId, string accessToken, TimeSpan ttl);
    Task<bool> IsActiveSessionAsync(string accountId, string accessToken);
}
