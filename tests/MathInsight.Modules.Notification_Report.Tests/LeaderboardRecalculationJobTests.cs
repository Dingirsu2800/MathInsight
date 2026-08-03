using MathInsight.Modules.Notification_Report.Contracts;
using MathInsight.Modules.Notification_Report.Entities;
using MathInsight.Modules.Notification_Report.Jobs;
using MathInsight.Modules.Notification_Report.Persistence;
using MathInsight.Modules.Notification_Report.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace MathInsight.Modules.Notification_Report.Tests;

public class LeaderboardRecalculationJobTests : IDisposable
{
    private readonly NotificationDbContext _db;
    private readonly Mock<ILeaderboardCacheService> _cache = new();

    public LeaderboardRecalculationJobTests()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new NotificationDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task RunAsync_CachesAllThreeGrades()
    {
        _db.Accounts.Add(new AccountReadOnly { AccountId = "s1", FirstName = "An", LastName = "Nguyen" });
        _db.CompetencyPoints.Add(new CompetencyPointReadOnly
        {
            CompetencyId = Guid.NewGuid().ToString(), StudentId = "s1", Grade = 10, Point = 8.0m
        });
        await _db.SaveChangesAsync();

        var job = new LeaderboardRecalculationJob(_db, _cache.Object);

        var totalCached = await job.RunAsync();

        Assert.Equal(1, totalCached);
        _cache.Verify(c => c.SetAsync(10, It.Is<IReadOnlyList<LeaderboardEntryDto>>(e => e.Count == 1), It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.SetAsync(11, It.Is<IReadOnlyList<LeaderboardEntryDto>>(e => e.Count == 0), It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.SetAsync(12, It.Is<IReadOnlyList<LeaderboardEntryDto>>(e => e.Count == 0), It.IsAny<CancellationToken>()), Times.Once);
    }
}
