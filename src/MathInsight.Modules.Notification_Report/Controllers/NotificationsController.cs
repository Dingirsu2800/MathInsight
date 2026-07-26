using System.Security.Claims;
using MathInsight.Modules.Notification_Report.Commands.MarkNotificationRead;
using MathInsight.Modules.Notification_Report.Errors;
using MathInsight.Modules.Notification_Report.Queries.GetNotifications;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MathInsight.Modules.Notification_Report.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public NotificationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var accountId = GetAuthenticatedAccountId();
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return Unauthorized(new { error = "Invalid or missing account identity." });
        }

        var result = await _mediator.Send(
            new GetNotificationsQuery(accountId, unreadOnly, pageIndex, pageSize),
            cancellationToken);

        return Ok(result.Value);
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkRead(string id, CancellationToken cancellationToken)
    {
        var accountId = GetAuthenticatedAccountId();
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return Unauthorized(new { error = "Invalid or missing account identity." });
        }

        var result = await _mediator.Send(new MarkNotificationReadCommand(id, accountId), cancellationToken);

        if (result.IsFailure)
        {
            return MapError(result.Error!);
        }

        return Ok();
    }

    private IActionResult MapError(Error error)
    {
        if (error == NotificationErrors.NotificationNotFound)
        {
            return NotFound(new ApiErrorResponse(error));
        }

        if (error == NotificationErrors.NotificationAccessForbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse(error));
        }

        return BadRequest(new ApiErrorResponse(error));
    }

    private string? GetAuthenticatedAccountId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
