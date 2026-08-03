using MathInsight.Modules.Notification_Report.Entities;
using MathInsight.Modules.Notification_Report.Persistence;
using MathInsight.Modules.Notification_Report.Queries.GetNotifications;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MathInsight.Modules.Notification_Report.Tests;

public class GetNotificationsQueryHandlerTests : IDisposable
{
    private readonly NotificationDbContext _db;
    private readonly GetNotificationsQueryHandler _handler;

    public GetNotificationsQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new NotificationDbContext(options);
        _handler = new GetNotificationsQueryHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private void Seed(string accountId, int count, bool read)
    {
        for (var i = 0; i < count; i++)
        {
            _db.Notifications.Add(new Notification
            {
                NotificationId = Guid.NewGuid().ToString(),
                UserId = accountId,
                Title = $"Title {i}",
                Content = "Content",
                IsRead = read,
                CreatedTime = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        _db.SaveChanges();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyCurrentAccountsNotifications()
    {
        Seed("account-1", 2, read: false);
        Seed("account-2", 3, read: false);

        var result = await _handler.Handle(new GetNotificationsQuery("account-1", false, 1, 20), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.TotalCount);
        Assert.All(result.Value.Items, item => Assert.Contains("Title", item.Title));
    }

    [Fact]
    public async Task Handle_UnreadOnly_FiltersReadNotifications()
    {
        Seed("account-1", 2, read: true);
        Seed("account-1", 3, read: false);

        var result = await _handler.Handle(new GetNotificationsQuery("account-1", true, 1, 20), CancellationToken.None);

        Assert.Equal(3, result.Value!.TotalCount);
        Assert.All(result.Value.Items, item => Assert.False(item.IsRead));
    }

    [Fact]
    public async Task Handle_Paging_ReturnsCorrectPageAndTotalPages()
    {
        Seed("account-1", 5, read: false);

        var result = await _handler.Handle(new GetNotificationsQuery("account-1", false, 2, 2), CancellationToken.None);

        Assert.Equal(2, result.Value!.PageIndex);
        Assert.Equal(2, result.Value.PageSize);
        Assert.Equal(5, result.Value.TotalCount);
        Assert.Equal(3, result.Value.TotalPages);
        Assert.Equal(2, result.Value.Items.Count);
    }
}
