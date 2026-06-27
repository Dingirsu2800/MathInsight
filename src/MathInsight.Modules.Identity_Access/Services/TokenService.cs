using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MathInsight.Modules.Identity_Access.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace MathInsight.Modules.Identity_Access.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string CreateAccessToken(Account account, out DateTime expiresAt, out string tokenId)
    {
        var signingKey = _configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured");

        var issuer = _configuration["Jwt:Issuer"] ?? "MathInsight";
        var audience = _configuration["Jwt:Audience"] ?? "MathInsightClient";

        var expirationMinutes = int.TryParse(_configuration["Jwt:ExpirationMinutes"],out var minutes) 
            ? minutes 
            : 60;

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
}

