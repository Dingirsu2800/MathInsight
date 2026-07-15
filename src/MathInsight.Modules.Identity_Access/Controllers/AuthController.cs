using MediatR;
using MathInsight.Modules.Identity_Access.Commands.ConfirmEmail;
using MathInsight.Modules.Identity_Access.Commands.ConfirmResetPassword;
using MathInsight.Modules.Identity_Access.Commands.GoogleCallback;
using MathInsight.Modules.Identity_Access.Commands.Login;
using MathInsight.Modules.Identity_Access.Commands.RefreshToken;
using MathInsight.Modules.Identity_Access.Commands.Register;
using MathInsight.Modules.Identity_Access.Commands.ResetPassword;
using MathInsight.Modules.Identity_Access.Contracts;
using MathInsight.Modules.Identity_Access.Contracts.Auth;
using MathInsight.Modules.Identity_Access.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using MathInsight.Modules.Identity_Access.Commands.Logout;
using MathInsight.Shared.Results;

namespace MathInsight.Modules.Identity_Access.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IOAuthStateStore _oauthStateStore;
    private readonly IGoogleOAuthService _googleOAuthService;
    private readonly GoogleOAuthOptions _googleOptions;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IMediator mediator,
        IOAuthStateStore oauthStateStore,
        IGoogleOAuthService googleOAuthService,
        GoogleOAuthOptions googleOptions,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _mediator = mediator;
        _oauthStateStore = oauthStateStore;
        _googleOAuthService = googleOAuthService;
        _googleOptions = googleOptions;
        _configuration = configuration;
        _logger = logger;
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

    // UC-07 (Flow A). Builds the Google consent URL with a server-side CSRF state and redirects
    // the browser to Google. Public.
    [HttpGet("google")]
    public async Task<IActionResult> GoogleLogin(CancellationToken cancellationToken)
    {
        var state = await _oauthStateStore.CreateAsync(cancellationToken);
        var authorizationUrl = _googleOAuthService.BuildAuthorizationUrl(state);

        // Diagnostic: ClientId comes from config (GoogleOAuth:ClientId). If it is empty, the URL
        // builder drops client_id and Google returns "Missing required parameter: client_id".
        _logger.LogInformation(
            "Google OAuth start — ClientId='{ClientId}' (length={Length}), authorization URL: {Url}",
            _googleOptions.ClientId,
            _googleOptions.ClientId?.Length ?? 0,
            authorizationUrl);

        if (string.IsNullOrEmpty(_googleOptions.ClientId))
        {
            _logger.LogError(
                "GoogleOAuth:ClientId is EMPTY at runtime. Set GoogleOAuth__ClientId (and __ClientSecret) " +
                "in the environment feeding the process (docker-compose defaults it to empty via " +
                "${{GoogleOAuth__ClientId:-}}, so the .env file must define it).");
        }

        return Redirect(authorizationUrl);
    }

    // UC-07 callback (the redirect URI registered in the Google Console). Verifies the CSRF state,
    // exchanges the code for tokens, and redirects the browser back to the frontend — carrying the
    // issued tokens on success, or ?error=google_failed on any failure. Public.
    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback(
            [FromQuery] string? code,
            [FromQuery] string? state,
            [FromQuery] string? error,
            CancellationToken cancellationToken)
    {
        var frontendBaseUrl = (_configuration["FrontendBaseUrl"] ?? string.Empty).TrimEnd('/');
        var failureRedirect = $"{frontendBaseUrl}/login?error=google_failed";

        // The user denied consent, Google returned an error, or required parameters are missing.
        if (!string.IsNullOrEmpty(error) ||
            string.IsNullOrWhiteSpace(code) ||
            string.IsNullOrWhiteSpace(state))
        {
            _logger.LogWarning(
                "Google callback rejected before handler — googleError={Error}, hasCode={HasCode}, hasState={HasState}.",
                error, !string.IsNullOrWhiteSpace(code), !string.IsNullOrWhiteSpace(state));
            return Redirect(failureRedirect);
        }

        // CSRF: the state must match a value we issued and have not yet consumed (single-use).
        if (!await _oauthStateStore.ConsumeAsync(state, cancellationToken))
        {
            _logger.LogWarning("Google callback: CSRF state did not match or was already consumed.");
            return Redirect(failureRedirect);
        }

        try
        {
            var result = await _mediator.Send(new GoogleOAuthCallbackCommand(code), cancellationToken);

            if (result.IsFailure)
            {
                _logger.LogWarning(
                    "Google callback: handler returned failure — code={Code}, message={Message}.",
                    result.Error?.Code, result.Error?.Message);
                return Redirect(failureRedirect);
            }

            var login = result.Value!;
            var successRedirect =
                $"{frontendBaseUrl}/auth/google/success" +
                $"?accessToken={Uri.EscapeDataString(login.AccessToken)}" +
                $"&refreshToken={Uri.EscapeDataString(login.RefreshToken)}" +
                $"&role={Uri.EscapeDataString(login.RoleName)}";

            return Redirect(successRedirect);
        }
        catch (Exception ex)
        {
            // Never let an exception surface as an unhandled 500 to the browser mid-redirect;
            // log it and fail closed to the frontend login page.
            _logger.LogError(ex, "Google callback: unhandled exception while resolving the account or issuing tokens.");
            return Redirect(failureRedirect);
        }
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
