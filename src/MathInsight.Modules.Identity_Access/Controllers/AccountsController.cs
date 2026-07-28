using System.Security.Claims;
using MathInsight.Modules.Identity_Access.Commands.ChangePassword;
using MathInsight.Modules.Identity_Access.Commands.UpdateProfile;
using MathInsight.Modules.Identity_Access.Contracts;
using MathInsight.Modules.Identity_Access.Contracts.Accounts;
using MathInsight.Modules.Identity_Access.Queries.GetProfile;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MathInsight.Modules.Identity_Access.Controllers;

/// <summary>
/// Account self-service (plan.md Phase 4). Every action operates on the caller's own account,
/// resolved from the access token — there is no account id in any route or body, so these
/// endpoints cannot be pointed at someone else's account.
/// </summary>
[ApiController]
[Route("api/v1/accounts")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AccountsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // UC-04. Any authenticated role.
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return Unauthorized(new ApiErrorResponse(
                AuthErrorCodes.TokenInvalid,
                "The access token does not identify an account."));
        }

        var result = await _mediator.Send(new GetProfileQuery(accountId), cancellationToken);

        if (result.IsFailure)
        {
            return ToProfileErrorResult(result.Error!);
        }

        return Ok(result.Value);
    }

    // UC-05. Any authenticated role. Partial update — omitted/null fields keep their stored
    // value. Returns the updated profile in the UC-04 shape.
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(
            [FromBody] UpdateProfileRequest request,
            CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return Unauthorized(new ApiErrorResponse(
                AuthErrorCodes.TokenInvalid,
                "The access token does not identify an account."));
        }

        var result = await _mediator.Send(
            new UpdateProfileCommand(
                accountId,
                request.FirstName,
                request.LastName,
                request.PhoneNumber,
                request.DateOfBirth,
                request.Gender,
                request.School,
                request.CurrentGrade,
                request.Biography,
                request.Specialty),
            cancellationToken);

        if (result.IsFailure)
        {
            return ToProfileErrorResult(result.Error!);
        }

        return Ok(result.Value);
    }

    // UC-03. Any authenticated role. On success every session for this account is revoked
    // (BR-15) and a fresh token pair is issued for the caller, returned in the login response
    // shape. Other devices are logged out; the caller stays signed in on the new tokens, which
    // it must store in place of the ones it sent.
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
            [FromBody] ChangePasswordRequest request,
            CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return Unauthorized(new ApiErrorResponse(
                AuthErrorCodes.TokenInvalid,
                "The access token does not identify an account."));
        }

        var result = await _mediator.Send(
            new ChangePasswordCommand(accountId, request.CurrentPassword, request.NewPassword),
            cancellationToken);

        if (result.IsFailure)
        {
            return ToChangePasswordErrorResult(result.Error!);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Reads the account id from the access token's claims (TokenService writes both
    /// <c>account_id</c> and NameIdentifier; NameIdentifier is the configured NameClaimType).
    /// </summary>
    private bool TryGetAccountId(out string accountId)
    {
        accountId = User.FindFirstValue("account_id")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? string.Empty;

        return !string.IsNullOrWhiteSpace(accountId);
    }

    // Both profile handlers can only fail with AUTH_TOKEN_INVALID (the token's account no longer
    // exists). The 409 duplicate-email mapping that used to live here went away with email
    // updates; it belongs on the future email-change endpoint instead.
    private IActionResult ToProfileErrorResult(Error error) =>
        Unauthorized(new ApiErrorResponse(error));

    // The three UC-03 failures are all bad request payloads, not authentication failures — the
    // caller's token is valid in every one of them. Only AUTH_TOKEN_INVALID (the token's account
    // no longer exists) falls through to 401.
    private IActionResult ToChangePasswordErrorResult(Error error)
    {
        if (error.Code == AuthErrorCodes.InvalidCurrentPassword ||
            error.Code == AuthErrorCodes.SamePassword ||
            error.Code == AuthErrorCodes.NoPasswordSet)
        {
            return BadRequest(new ApiErrorResponse(error));
        }

        return Unauthorized(new ApiErrorResponse(error));
    }
}
