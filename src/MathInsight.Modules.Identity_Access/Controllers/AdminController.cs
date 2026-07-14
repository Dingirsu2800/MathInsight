using MathInsight.Modules.Identity_Access.Commands.AdjustPermission;
using MathInsight.Modules.Identity_Access.Commands.ImportAccounts;
using MathInsight.Modules.Identity_Access.Commands.ManualCreateAccount;
using MathInsight.Modules.Identity_Access.Commands.ResolveApplication;
using MathInsight.Modules.Identity_Access.Commands.ToggleAccountStatus;
using MathInsight.Modules.Identity_Access.Commands.UpdateAccount;
using MathInsight.Modules.Identity_Access.Commands.UpdateRole;
using MathInsight.Modules.Identity_Access.Contracts.Admin;
using MathInsight.Modules.Identity_Access.Queries.GetAccountList;
using MathInsight.Modules.Identity_Access.Queries.GetRoles;
using MathInsight.Modules.Identity_Access.Queries.GetTeacherApplicationDetail;
using MathInsight.Modules.Identity_Access.Queries.GetTeacherApplications;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MathInsight.Modules.Identity_Access.Controllers;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/v1/admin")]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string CurrentAccountId =>
        User.FindFirst("account_id")?.Value
        ?? throw new InvalidOperationException("Missing account_id claim.");

    // UC-09
    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts(
        [FromQuery] int pageIndex,
        [FromQuery] int pageSize,
        [FromQuery] string? role,
        [FromQuery] bool? isActive,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetAccountListQuery(pageIndex, pageSize, role, isActive, search),
            cancellationToken);

        return ToActionResult(result);
    }

    // UC-11
    [HttpPost("accounts/manual")]
    public async Task<IActionResult> CreateAccountManually(
        [FromBody] CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ManualCreateAccountCommand(
                request.Username,
                request.Email,
                request.Password,
                request.FirstName,
                request.LastName,
                request.PhoneNumber,
                request.DateOfBirth,
                request.RoleName),
            cancellationToken);

        return ToActionResult(result);
    }

    // UC-12
    [HttpPost("accounts/import")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> ImportAccounts(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ApiErrorResponse("INVALID_EXCEL_FILE", "No file uploaded."));

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);

        var result = await _mediator.Send(new ImportAccountsCommand(memoryStream.ToArray()), cancellationToken);

        return ToActionResult(result);
    }

    // UC-13
    [HttpPut("accounts/{id}")]
    public async Task<IActionResult> UpdateAccount(
        string id,
        [FromBody] UpdateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new UpdateAccountCommand(id, request.FirstName, request.LastName, request.Email, request.RoleId),
            cancellationToken);

        return ToActionResult(result);
    }

    // UC-14
    [HttpPut("accounts/{id}/status")]
    public async Task<IActionResult> ToggleAccountStatus(
        string id,
        [FromBody] ToggleAccountStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ToggleAccountStatusCommand(id, request.IsActive, CurrentAccountId),
            cancellationToken);

        return ToActionResult(result);
    }

    // UC-10 (list)
    [HttpGet("applications")]
    public async Task<IActionResult> GetApplications(
        [FromQuery] int pageIndex,
        [FromQuery] int pageSize,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetTeacherApplicationsQuery(pageIndex, pageSize, status),
            cancellationToken);

        return ToActionResult(result);
    }

    // UC-10 (detail)
    [HttpGet("applications/{id}")]
    public async Task<IActionResult> GetApplicationDetail(string id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetTeacherApplicationDetailQuery(id), cancellationToken);
        return ToActionResult(result);
    }

    // UC-15
    [HttpPost("applications/{id}/resolve")]
    public async Task<IActionResult> ResolveApplication(
        string id,
        [FromBody] ResolveApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ResolveApplicationCommand(id, request.Approve, request.ReviewComments, CurrentAccountId),
            cancellationToken);

        return ToActionResult(result);
    }

    // Read-side support for UC-16/UC-17
    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetRolesQuery(), cancellationToken);
        return ToActionResult(result);
    }

    // UC-17
    [HttpPut("roles/{roleId}")]
    public async Task<IActionResult> UpdateRole(
        string roleId,
        [FromBody] UpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new UpdateRoleCommand(roleId, request.RoleName, request.Description),
            cancellationToken);

        return ToActionResult(result);
    }

    // UC-16
    [HttpPut("roles/{roleId}/permissions")]
    public async Task<IActionResult> AdjustPermissions(
        string roleId,
        [FromBody] AdjustPermissionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new AdjustPermissionCommand(roleId, request.PermissionIds, CurrentAccountId),
            cancellationToken);

        return ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        var error = result.Error!;
        return error.Code switch
        {
            "ACCOUNT_NOT_FOUND" or "APPLICATION_NOT_FOUND" or "ROLE_NOT_FOUND" or "PERMISSION_NOT_FOUND"
                => NotFound(new ApiErrorResponse(error)),
            "EMAIL_ALREADY_EXISTS" or "USERNAME_ALREADY_EXISTS" or "ROLE_NAME_ALREADY_EXISTS"
                => Conflict(new ApiErrorResponse(error)),
            "CANNOT_DEACTIVATE_SELF" or "CANNOT_REMOVE_OWN_ADMIN_PERMISSION" or "SYSTEM_ROLE_RENAME_FORBIDDEN"
                => StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse(error)),
            _ => BadRequest(new ApiErrorResponse(error))
        };
    }
}
