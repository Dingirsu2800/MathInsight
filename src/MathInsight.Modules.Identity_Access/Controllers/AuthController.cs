using MediatR;
using MathInsight.Modules.Identity_Access.Commands.ConfirmEmail;
using MathInsight.Modules.Identity_Access.Commands.ConfirmResetPassword;
using MathInsight.Modules.Identity_Access.Commands.Login;
using MathInsight.Modules.Identity_Access.Commands.RefreshToken;
using MathInsight.Modules.Identity_Access.Commands.Register;
using MathInsight.Modules.Identity_Access.Commands.ResetPassword;
using MathInsight.Modules.Identity_Access.Contracts;
using MathInsight.Modules.Identity_Access.Contracts.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using MathInsight.Modules.Identity_Access.Commands.Logout;
using MathInsight.Shared.Results;

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

        if (result.IsFailure)
        {
            return ToAuthErrorResult(result.Error!);
        }

        return Ok(result.Value);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
            [FromBody] RefreshTokenRequest request,
            CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new RefreshTokenCommand(request.RefreshToken),
            cancellationToken);

        if (result.IsFailure)
        {
            return ToAuthErrorResult(result.Error!);
        }

        return Ok(result.Value);
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
            [FromBody] ResetPasswordRequest request,
            CancellationToken cancellationToken)
    {
        // Fire-and-forget from the caller's view: the handler always succeeds so we never reveal
        // whether the email is registered (UC-06 enumeration protection).
        await _mediator.Send(new ResetPasswordCommand(request.Email), cancellationToken);

        return Ok(new { message = "If an account with that email exists, a password reset link has been sent." });
    }

    [HttpPost("confirm-reset-password")]
    public async Task<IActionResult> ConfirmResetPassword(
            [FromBody] ConfirmResetPasswordRequest request,
            CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ConfirmResetPasswordCommand(request.Token, request.NewPassword),
            cancellationToken);

        if (result.IsFailure)
        {
            return ToAuthErrorResult(result.Error!);
        }

        return Ok(new { message = "Your password has been reset. Please log in with your new password." });
    }

    [HttpPost("register/student")]
    public async Task<IActionResult> RegisterStudent(
            [FromBody] StudentRegisterRequest request,
            CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new StudentRegisterCommand(
                request.Username,
                request.Email,
                request.Password,
                request.FirstName,
                request.LastName,
                request.Gender,
                request.School,
                request.CurrentGrade),
            cancellationToken);

        if (result.IsFailure)
        {
            return ToAuthErrorResult(result.Error!);
        }

        return Accepted(new { message = "Registration received. Please check your email to confirm your account." });
    }

    [HttpPost("register/teacher")]
    public async Task<IActionResult> RegisterTeacher(
            [FromForm] TeacherRegisterRequest request,
            CancellationToken cancellationToken)
    {
        // Stream stays open for the duration of the upload inside the handler.
        await using var certificateStream = request.Certificate.OpenReadStream();

        var result = await _mediator.Send(
            new TeacherRegisterCommand(
                request.Username,
                request.Email,
                request.Password,
                request.FirstName,
                request.LastName,
                request.Biography,
                certificateStream,
                request.Certificate.FileName,
                request.Certificate.ContentType,
                request.Certificate.Length),
            cancellationToken);

        if (result.IsFailure)
        {
            return ToAuthErrorResult(result.Error!);
        }

        return Accepted(new { message = "Registration received. Please check your email to confirm your account." });
    }

    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(
            [FromBody] ConfirmEmailRequest request,
            CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ConfirmEmailCommand(request.Token),
            cancellationToken);

        if (result.IsFailure)
        {
            return ToAuthErrorResult(result.Error!);
        }

        return Ok(new { message = "Email confirmed. Your account is now active." });
    }

    // No [Authorize]: logout must work even when the access token is expired or missing (BR-10).
    // The refresh token in the body identifies the session to revoke; the handler looks it up in
    // Redis. The jti/exp below are read decode-only from the (possibly expired) bearer token — not
    // required, and never a reason to reject the request.
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
            [FromBody] LogoutRequest request,
            CancellationToken cancellationToken)
    {
        string? accessTokenJti = null;
        DateTime? accessTokenExpiresAtUtc = null;

        if (TryReadBearerToken(out var bearerToken))
        {
            accessTokenJti = bearerToken.Id; // the "jti" claim
            accessTokenExpiresAtUtc = bearerToken.ValidTo == DateTime.MinValue
                ? null
                : bearerToken.ValidTo;
        }

        await _mediator.Send(
            new LogoutCommand(request.RefreshToken, accessTokenJti, accessTokenExpiresAtUtc),
            cancellationToken);

        // Idempotent: always a success, even for an unknown/invalid refresh token, so we never
        // reveal whether the session existed.
        return NoContent();
    }

    /// <summary>
    /// Decodes the bearer token from the Authorization header WITHOUT validating its signature or
    /// lifetime, so an expired access token can still surrender its jti/exp for blacklisting at
    /// logout. Returns false when the header is absent or the token is not a readable JWT.
    /// </summary>
    private bool TryReadBearerToken(out JwtSecurityToken token)
    {
        token = null!;

        var authorization = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization) ||
            !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rawToken = authorization["Bearer ".Length..].Trim();

        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(rawToken))
        {
            return false;
        }

        try
        {
            token = handler.ReadJwtToken(rawToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private IActionResult ToAuthErrorResult(Error error)
    {
        if (error.Code == AuthErrorCodes.AccountLocked)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new ApiErrorResponse(error));
        }

        if (error.Code == AuthErrorCodes.AccountDeactivated ||
            error.Code == AuthErrorCodes.ApplicationPending)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse(error));
        }

        if (error.Code == AuthErrorCodes.EmailAlreadyConfirmed)
        {
            return Conflict(new ApiErrorResponse(error));
        }

        if (error.Code == AuthErrorCodes.TokenExpired)
        {
            return StatusCode(StatusCodes.Status410Gone, new ApiErrorResponse(error));
        }

        if (error.Code == AuthErrorCodes.CertificateInvalid)
        {
            return BadRequest(new ApiErrorResponse(error));
        }

        if (error.Code == AuthErrorCodes.ApplicationRejected)
        {
            // BR-13: rejection returns the admin's review comments alongside the code.
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = error.Code,
                message = "Teacher application was rejected.",
                review_comments = error.Message
            });
        }

        // AUTH_INVALID_CREDENTIALS, AUTH_TOKEN_INVALID, and anything else → 401.
        return Unauthorized(new ApiErrorResponse(error));
    }
}
