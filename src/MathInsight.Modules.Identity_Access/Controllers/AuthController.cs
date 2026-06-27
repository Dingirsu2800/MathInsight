using MediatR;
using MathInsight.Modules.Identity_Access.Commands.Login;
using MathInsight.Modules.Identity_Access.Contracts.Auth;
using Microsoft.AspNetCore.Mvc;

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
}

