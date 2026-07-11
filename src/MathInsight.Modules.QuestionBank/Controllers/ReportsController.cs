using System.Security.Claims;
using MathInsight.Modules.QuestionBank.Commands.AdminApproveQuestionReport;
using MathInsight.Modules.QuestionBank.Commands.AdminRejectQuestionReport;
using MathInsight.Modules.QuestionBank.Commands.HandleQuestionReport;
using MathInsight.Modules.QuestionBank.Commands.ReportQuestion;
using MathInsight.Modules.QuestionBank.Commands.SubmitQuestionReportReview;
using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Queries.GetOwnedReportedQuestions;
using MathInsight.Modules.QuestionBank.Queries.GetAdminQuestionReports;
using MathInsight.Modules.QuestionBank.Queries.GetQuestionReports;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MathInsight.Modules.QuestionBank.Controllers;

[ApiController]
[Authorize(Roles = "Student,Expert,Admin")]
[Route("api/question-bank")]
public sealed class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReportsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("questions/{questionId}/reports")]
    public async Task<IActionResult> ReportQuestion(
        string questionId,
        [FromBody] ReportQuestionRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new ApiErrorResponse(QuestionBankErrors.ReportReasonRequired));

        var accountId = GetAccountId();
        var role = GetRole();
        if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(role))
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(
            new ReportQuestionCommand(questionId, request, accountId, role),
            cancellationToken);

        if (result.IsFailure)
            return ToReportErrorResult(result.Error!);

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [Authorize(Roles = "Expert")]
    [HttpGet("reports/mine")]
    public async Task<IActionResult> GetOwnedReportedQuestions(
        [FromQuery] string? status,
        [FromQuery] int pageIndex,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        var expertId = GetAccountId();
        if (string.IsNullOrWhiteSpace(expertId))
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(
            new GetOwnedReportedQuestionsQuery(expertId, status, pageIndex, pageSize),
            cancellationToken);

        if (result.IsFailure)
            return ToReportErrorResult(result.Error!);

        return Ok(result.Value);
    }

    [Authorize(Roles = "Expert")]
    [HttpGet("questions/{questionId}/reports")]
    public async Task<IActionResult> GetQuestionReports(
        string questionId,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var expertId = GetAccountId();
        if (string.IsNullOrWhiteSpace(expertId))
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(
            new GetQuestionReportsQuery(questionId, expertId, status),
            cancellationToken);

        if (result.IsFailure)
            return ToReportErrorResult(result.Error!);

        return Ok(result.Value);
    }

    [Authorize(Roles = "Expert")]
    [HttpPatch("reports/{reportId}")]
    public async Task<IActionResult> HandleQuestionReport(
        string reportId,
        [FromBody] HandleQuestionReportRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new ApiErrorResponse(QuestionBankErrors.ReportStatusInvalid));

        var expertId = GetAccountId();
        if (string.IsNullOrWhiteSpace(expertId))
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(
            new HandleQuestionReportCommand(reportId, request, expertId),
            cancellationToken);

        if (result.IsFailure)
            return ToReportErrorResult(result.Error!);

        return Ok(result.Value);
    }

    [Authorize(Roles = "Expert")]
    [HttpPost("reports/{reportId}/submit-review")]
    public async Task<IActionResult> SubmitQuestionReportReview(
        string reportId,
        CancellationToken cancellationToken)
    {
        var expertId = GetAccountId();
        if (string.IsNullOrWhiteSpace(expertId))
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(
            new SubmitQuestionReportReviewCommand(reportId, expertId),
            cancellationToken);

        if (result.IsFailure)
            return ToReportErrorResult(result.Error!);

        return Ok(result.Value);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("admin/reports/mine")]
    public async Task<IActionResult> GetAdminQuestionReports(
        [FromQuery] string? status,
        [FromQuery] int pageIndex,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        var adminId = GetAccountId();
        if (string.IsNullOrWhiteSpace(adminId))
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(
            new GetAdminQuestionReportsQuery(adminId, status, pageIndex, pageSize),
            cancellationToken);

        if (result.IsFailure)
            return ToReportErrorResult(result.Error!);

        return Ok(result.Value);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("admin/reports/{reportId}/approve")]
    public async Task<IActionResult> ApproveQuestionReport(
        string reportId,
        CancellationToken cancellationToken)
    {
        var adminId = GetAccountId();
        if (string.IsNullOrWhiteSpace(adminId))
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(
            new AdminApproveQuestionReportCommand(reportId, adminId),
            cancellationToken);

        if (result.IsFailure)
            return ToReportErrorResult(result.Error!);

        return Ok(result.Value);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("admin/reports/{reportId}/reject")]
    public async Task<IActionResult> RejectQuestionReport(
        string reportId,
        [FromBody] AdminRejectQuestionReportRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new ApiErrorResponse(QuestionBankErrors.ReviewNoteRequired));

        var adminId = GetAccountId();
        if (string.IsNullOrWhiteSpace(adminId))
            return Unauthorized(new ApiErrorResponse(ApplicationErrors.AuthInvalidToken));

        var result = await _mediator.Send(
            new AdminRejectQuestionReportCommand(reportId, request, adminId),
            cancellationToken);

        if (result.IsFailure)
            return ToReportErrorResult(result.Error!);

        return Ok(result.Value);
    }

    private string? GetAccountId()
    {
        return User.FindFirst("account_id")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    private string? GetRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value
            ?? User.FindFirst("role")?.Value;
    }

    private IActionResult ToReportErrorResult(Error error)
    {
        if (error == QuestionBankErrors.QuestionNotFound || error == QuestionBankErrors.ReportNotFound)
            return NotFound(new ApiErrorResponse(error));

        if (error == QuestionBankErrors.QuestionSelfReportForbidden ||
            error == QuestionBankErrors.ReportAccessForbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse(error));
        }

        if (error == QuestionBankErrors.ReportAlreadyPending ||
            error == QuestionBankErrors.ReportAlreadyHandled ||
            error == QuestionBankErrors.QuestionNotReportable ||
            error == QuestionBankErrors.AdminReportWorkflowAlreadyExists ||
            error == QuestionBankErrors.AdminReportRequiresReview)
        {
            return Conflict(new ApiErrorResponse(error));
        }

        return BadRequest(new ApiErrorResponse(error));
    }
}
