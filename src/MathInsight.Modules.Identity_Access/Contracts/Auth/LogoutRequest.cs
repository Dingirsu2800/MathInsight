using System.ComponentModel.DataAnnotations;

namespace MathInsight.Modules.Identity_Access.Contracts.Auth;

public class LogoutRequest
{
    /// <summary>
    /// The refresh token to revoke for this session (BR-10). Required: a logout without it
    /// returns 400. If it were optional, the refresh token would survive its full 7-day lifetime
    /// and the session would not actually end.
    /// </summary>
    [Required]
    public string RefreshToken { get; set; } = default!;
}
