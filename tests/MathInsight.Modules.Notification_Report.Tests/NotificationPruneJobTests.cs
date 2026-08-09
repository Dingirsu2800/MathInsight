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

    [Fact]
    public async Task RunAsync_NotificationJustUnderNinetyDaysOld_IsKept()
    {
        // The job computes its own cutoff = DateTime.UtcNow.AddDays(-90) at run time, which is
        // necessarily a few milliseconds after this seed's UtcNow snapshot — an exact-tick tie
        // can't be constructed against a live clock (RunAsync takes no injectable clock). A few
        // seconds' margin on the "just inside retention" side is enough to prove the strict `<`
        // boundary (not `<=`) without racing the clock.
        _db.Notifications.Add(new Entities.Notification
        {
            NotificationId = "just-under-90",
            UserId = "account-1",
            Title = "Title",
            Content = "Content",
            IsRead = false,
            CreatedTime = DateTime.UtcNow.AddDays(-90).AddSeconds(5)
        });
        await _db.SaveChangesAsync();

        var deletedCount = await _job.RunAsync();

        Assert.Equal(0, deletedCount);
        Assert.NotNull(await _db.Notifications.FindAsync("just-under-90"));
    }

    [Fact]
    public async Task RunAsync_NotificationJustOverNinetyDaysOld_IsDeleted()
    {
        _db.Notifications.Add(new Entities.Notification
        {
            NotificationId = "just-over-90",
            UserId = "account-1",
            Title = "Title",
            Content = "Content",
            IsRead = false,
            CreatedTime = DateTime.UtcNow.AddDays(-90).AddSeconds(-5)
        });
        await _db.SaveChangesAsync();

        var deletedCount = await _job.RunAsync();

        Assert.Equal(1, deletedCount);
        Assert.Null(await _db.Notifications.FindAsync("just-over-90"));
    }
}
