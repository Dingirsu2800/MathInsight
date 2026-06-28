using MediatR;
using MathInsight.Modules.Identity_Access.Commands.Login;
using MathInsight.Modules.Identity_Access.Contracts.Auth;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using MathInsight.Modules.Identity_Access.Commands.Logout;
using Microsoft.AspNetCore.Authorization;

namespace MathInsight.Modules.Identity_Access.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
            [FromBody] LoginRequest request,
            CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new LoginCommand(request.UsernameOrEmail, request.Password),
            cancellationToken);

        if (result is null)
            return Unauthorized(new
            {
                code = "AUTH_INVALID_CREDENTIALS",
                message = "Invalid username/email or password."
            });

        return Ok(result);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var tokenId = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value
            ?? User.FindFirst("jti")?.Value;

        var expirationValue = User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value
            ?? User.FindFirst("exp")?.Value;

        if (string.IsNullOrWhiteSpace(tokenId) ||
            !long.TryParse(expirationValue, out var expirationUnixSeconds))
        {
            return Unauthorized(new
            {
                code = "AUTH_INVALID_TOKEN",
                message = "Invalid or missing token claims."
            });
        }

        var expiresAtUtc = DateTimeOffset
            .FromUnixTimeSeconds(expirationUnixSeconds)
            .UtcDateTime;

        await _mediator.Send(
            new LogoutCommand(tokenId, expiresAtUtc),
            cancellationToken);

        return NoContent();
    }
}

