using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace MathInsight.Modules.Notification_Report.Hubs;

/// <summary>
/// BR-20: real-time notification push, mapped at /hubs/notification. SignalR's default
/// IUserIdProvider reads ClaimTypes.NameIdentifier from the connection's principal — the same
/// claim TokenService writes the account id to — so NotificationService can target a user with
/// Clients.User(accountId) without any custom group management here.
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public override Task OnConnectedAsync()
    {
        _logger.LogInformation(
            "SignalR connection established for account {AccountId}.",
            Context.UserIdentifier);
        return base.OnConnectedAsync();
    }
}
