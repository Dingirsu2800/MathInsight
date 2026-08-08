using System.Security.Claims;
using MathInsight.Modules.Identity_Access.Authorization;
using MathInsight.Modules.Identity_Access.Commands.ResubmitTeacherApplication;
using MathInsight.Modules.Identity_Access.Commands.UpdateMyTeacherApplication;
using MathInsight.Modules.Identity_Access.Contracts;
using MathInsight.Modules.Identity_Access.Contracts.Teacher;
using MathInsight.Modules.Identity_Access.Queries.GetMyTeacherApplication;
using MathInsight.Modules.Identity_Access.Services;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MathInsight.Modules.Identity_Access.Controllers;

/// <summary>
/// UC-08 self-service. Guarded by "TeacherApplicant" — role Teacher, approval NOT required — because
/// these three endpoints exist precisely for teachers whose application is Pending or Rejected.
/// They are the ONLY endpoints such an account may reach; everything that exposes real teacher
/// functionality uses the "TeacherApproved" policy instead.
///
/// Every action resolves the caller's account id from the access token and the handlers verify the
/// application belongs to it, so no route value can reach another teacher's application.
/// </summary>
[ApiController]
[Route("api/v1/teacher/application")]
[Authorize(Policy = IdentityAuthorizationPolicies.TeacherApplicant)]
public class TeacherApplicationController : ControllerBase
{
    private readonly IMediator _mediator;

    public TeacherApplicationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>UC-08. The caller's own application — no id parameter, by design.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMyApplication(CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return Unauthorized(new ApiErrorResponse(
                AuthErrorCodes.TokenInvalid,
                "The access token does not identify an account."));
        }

        var result = await _mediator.Send(new GetMyTeacherApplicationQuery(accountId), cancellationToken);

        return ToActionResult(result);
    }

    /// <summary>
    /// UC-08. Edits a rejected application. Multipart, mirroring the registration endpoint: the
    /// certificate streams stay open for the duration of the uploads inside the handler, and
    /// SizeInBytes must come from IFormFile.Length so the per-file 10 MB gate applies (BR-05).
    /// </summary>
    [HttpPut("{applicationId}")]
    [RequestSizeLimit(64L * 1024 * 1024)]
    public async Task<IActionResult> UpdateMyApplication(
        string applicationId,
        [FromForm] UpdateMyApplicationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return Unauthorized(new ApiErrorResponse(
                AuthErrorCodes.TokenInvalid,
                "The access token does not identify an account."));
        }

        var certificateStreams = new List<Stream>(request.Certificates.Count);

        try
        {
            var certificates = new List<CertificateUploadRequest>(request.Certificates.Count);

            foreach (var file in request.Certificates)
            {
                var stream = file.OpenReadStream();
                certificateStreams.Add(stream);
                certificates.Add(new CertificateUploadRequest(
                    stream,
                    file.FileName,
                    file.ContentType,
                    file.Length));
            }

            var result = await _mediator.Send(
                new UpdateMyTeacherApplicationCommand(
                    applicationId,
                    accountId,
                    request.FirstName,
                    request.LastName,
                    request.PhoneNumber,
                    request.Biography,
                    request.KeptDocumentsUrls,
                    certificates),
                cancellationToken);

            return ToActionResult(result);
        }
        finally
        {
            foreach (var stream in certificateStreams)
            {
                await stream.DisposeAsync();
            }
        }
    }

    /// <summary>UC-08. Puts a rejected application back into the Admin queue as Pending.</summary>
    [HttpPost("{applicationId}/resubmit")]
    public async Task<IActionResult> ResubmitMyApplication(
        string applicationId,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return Unauthorized(new ApiErrorResponse(
                AuthErrorCodes.TokenInvalid,
                "The access token does not identify an account."));
        }

        var result = await _mediator.Send(
            new ResubmitTeacherApplicationCommand(applicationId, accountId),
            cancellationToken);

        return ToActionResult(result);
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

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        var error = result.Error!;

        return error.Code switch
        {
            "APPLICATION_NOT_FOUND" => NotFound(new ApiErrorResponse(error)),
            "APPLICATION_FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse(error)),
            "APPLICATION_NOT_EDITABLE" => Conflict(new ApiErrorResponse(error)),
            AuthErrorCodes.PhoneNumberAlreadyUsed => Conflict(new ApiErrorResponse(error)),
            _ => BadRequest(new ApiErrorResponse(error)),
        };
    }
}
