using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MathInsight.Modules.Identity_Access.Entities;
using MathInsight.Modules.Identity_Access.Services.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace MathInsight.Modules.Identity_Access.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly IAuthSessionService _authSessionService;

    public TokenService(IConfiguration configuration, IAuthSessionService authSessionService)
    {
        _configuration = configuration;
        _authSessionService = authSessionService;
    }

    public string CreateAccessToken(Account account, out DateTime expiresAt, out string tokenId)
    {
        var signingKey = _configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured");

        var issuer = _configuration["Jwt:Issuer"] ?? "MathInsight";
        var audience = _configuration["Jwt:Audience"] ?? "MathInsightClient";

        // DD-02: access tokens are short-lived (15 minutes).
        var expirationMinutes = int.TryParse(_configuration["Jwt:ExpirationMinutes"],out var minutes)
            ? minutes
            : 15;

        expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);

        tokenId = Guid.NewGuid().ToString("N");

        var claims = new List<Claim>
        {
            new("account_id",account.AccountId),
            new("role",account.Role.RoleName),
            new("email",account.Email),

            new(JwtRegisteredClaimNames.Jti,tokenId),
            new(ClaimTypes.NameIdentifier, account.AccountId),
            new(ClaimTypes.Name, account.Username),
            new(ClaimTypes.Email, account.Email),
            new(ClaimTypes.Role, account.Role.RoleName),
        };

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<string> IssueRefreshTokenAsync(
        string accountId,
        string accessTokenJti,
        DateTime accessTokenExpiresAtUtc,
        CancellationToken cancellationToken)
    {
        var refreshToken = Guid.NewGuid().ToString("N");

        await _authSessionService.StoreRefreshSessionAsync(
            accountId,
            refreshToken,
            accessTokenJti,
            accessTokenExpiresAtUtc,
            GetRefreshTokenLifetime());

        return refreshToken;
    }

    public async Task<RefreshTokenResult?> RotateRefreshTokenAsync(
        string currentRefreshToken,
        string newAccessTokenJti,
        DateTime newAccessTokenExpiresAtUtc,
        CancellationToken cancellationToken)
    {
        var accountId = await _authSessionService.GetAccountIdByRefreshTokenAsync(currentRefreshToken);

        if (accountId is null)
        {
            return null;
        }

        // Delete the presented token first so it can never be used twice (single-use rotation).
        await _authSessionService.RemoveRefreshSessionAsync(accountId, currentRefreshToken);

        var newRefreshToken = await IssueRefreshTokenAsync(
            accountId,
            newAccessTokenJti,
            newAccessTokenExpiresAtUtc,
            cancellationToken);

        return new RefreshTokenResult(accountId, newRefreshToken);
    }

    public Task BlacklistAccessTokenAsync(string accessTokenJti, TimeSpan remainingLifetime)
        => _authSessionService.BlacklistTokenAsync(accessTokenJti, remainingLifetime);

    private TimeSpan GetRefreshTokenLifetime()
    {
        // DD-02: refresh tokens live 7 days.
        var days = int.TryParse(_configuration["Jwt:RefreshTokenDays"], out var configured)
            ? configured
            : 7;

        return TimeSpan.FromDays(days);
    }
}

