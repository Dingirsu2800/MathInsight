using MathInsight.Modules.Notification_Report.Entities;
using MathInsight.Modules.Notification_Report.Jobs;
using MathInsight.Modules.Notification_Report.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MathInsight.Modules.Notification_Report.Tests;

public class NotificationPruneJobTests : IDisposable
{
    private readonly NotificationDbContext _db;
    private readonly NotificationPruneJob _job;

    public NotificationPruneJobTests()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new NotificationDbContext(options);
        _job = new NotificationPruneJob(_db);
    }

    public void Dispose() => _db.Dispose();

    private void Seed(string id, int ageDays)
    {
        _db.Notifications.Add(new Notification
        {
            NotificationId = id,
            UserId = "account-1",
            Title = "Title",
            Content = "Content",
            IsRead = false,
            CreatedTime = DateTime.UtcNow.AddDays(-ageDays)
        });
    }

    [Fact]
    public async Task RunAsync_DeletesOlderThan90Days_KeepsNewer()
    {
        Seed("expired", ageDays: 91);
        Seed("retained", ageDays: 89);
        await _db.SaveChangesAsync();

        var deletedCount = await _job.RunAsync();

        Assert.Equal(1, deletedCount);
        Assert.Null(await _db.Notifications.FindAsync("expired"));
        Assert.NotNull(await _db.Notifications.FindAsync("retained"));
    }

    [Fact]
    public async Task RunAsync_NothingExpired_ReturnsZero()
    {
        Seed("retained", ageDays: 10);
        await _db.SaveChangesAsync();

        var deletedCount = await _job.RunAsync();

        Assert.Equal(0, deletedCount);
    }
}
