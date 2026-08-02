using MathInsight.Modules.Notification_Report.Errors;
using MathInsight.Modules.Notification_Report.Hubs;
using MathInsight.Modules.Notification_Report.Persistence;
using MathInsight.Modules.Notification_Report.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace MathInsight.Modules.Notification_Report.Tests;

/// <summary>
/// Uses EF Core InMemory for the DbContext (mirrors Gamification's StreakServiceTests) and a Moq
/// double for IHubContext&lt;NotificationHub&gt; since SignalR's SendAsync is an extension method
/// over IClientProxy.SendCoreAsync, which is the member Moq can actually intercept.
/// </summary>
public class NotificationServiceTests : IDisposable
{
    private readonly NotificationDbContext _db;
    private readonly Mock<IClientProxy> _clientProxy = new();
    private readonly NotificationService _service;

    public NotificationServiceTests()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new NotificationDbContext(options);

        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.User(It.IsAny<string>())).Returns(_clientProxy.Object);

        var hubContext = new Mock<IHubContext<NotificationHub>>();
        hubContext.Setup(h => h.Clients).Returns(clients.Object);

        _service = new NotificationService(_db, hubContext.Object);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task SendAsync_InsertsNotificationRow()
    {
        var id = await _service.SendAsync("account-1", "Title", "Content", "/link");

        var stored = await _db.Notifications.AsNoTracking().FirstOrDefaultAsync(n => n.NotificationId == id);
        Assert.NotNull(stored);
        Assert.Equal("account-1", stored!.UserId);
        Assert.Equal("Title", stored.Title);
        Assert.Equal("Content", stored.Content);
        Assert.Equal("/link", stored.Link);
        Assert.False(stored.IsRead);
    }

    [Fact]
    public async Task SendAsync_PushesToTheTargetAccountOverSignalR()
    {
        await _service.SendAsync("account-1", "Title", "Content");

        _clientProxy.Verify(
            proxy => proxy.SendCoreAsync(
                "ReceiveNotification",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MarkReadAsync_Owner_SetsIsReadTrue()
    {
        var id = await _service.SendAsync("account-1", "Title", "Content");

        var result = await _service.MarkReadAsync(id, "account-1");

        Assert.True(result.IsSuccess);
        var stored = await _db.Notifications.AsNoTracking().FirstAsync(n => n.NotificationId == id);
        Assert.True(stored.IsRead);
    }

    [Fact]
    public async Task MarkReadAsync_NotOwner_ReturnsForbidden()
    {
        var id = await _service.SendAsync("account-1", "Title", "Content");

        var result = await _service.MarkReadAsync(id, "account-2");

        Assert.True(result.IsFailure);
        Assert.Equal(NotificationErrors.NotificationAccessForbidden, result.Error);
    }

    [Fact]
    public async Task MarkReadAsync_Unknown_ReturnsNotFound()
    {
        var result = await _service.MarkReadAsync("does-not-exist", "account-1");

        Assert.True(result.IsFailure);
        Assert.Equal(NotificationErrors.NotificationNotFound, result.Error);
    }
}
