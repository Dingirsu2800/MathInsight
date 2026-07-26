namespace MathInsight.Modules.Notification_Report.Entities;

public class Notification
{
    public string NotificationId { get; set; } = default!;
    public string UserId { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Content { get; set; } = default!;
    public string? Link { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedTime { get; set; }
}
