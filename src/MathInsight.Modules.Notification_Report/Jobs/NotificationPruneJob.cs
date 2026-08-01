using MathInsight.Modules.Notification_Report.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Notification_Report.Jobs;

/// <summary>BR-21: deletes notifications older than 90 days.</summary>
public class NotificationPruneJob
{
    private const int RetentionDays = 90;

    private readonly NotificationDbContext _dbContext;

    public NotificationPruneJob(NotificationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);

        var expired = await _dbContext.Notifications
            .Where(n => n.CreatedTime < cutoff)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
        {
            return 0;
        }

        _dbContext.Notifications.RemoveRange(expired);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }
}
