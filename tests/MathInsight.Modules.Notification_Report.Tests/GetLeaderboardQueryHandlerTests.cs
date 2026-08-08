using MathInsight.Modules.Notification_Report.Contracts;
using MathInsight.Modules.Notification_Report.Entities;
using MathInsight.Modules.Notification_Report.Errors;
using MathInsight.Modules.Notification_Report.Persistence;
using MathInsight.Modules.Notification_Report.Queries.GetLeaderboard;
using MathInsight.Modules.Notification_Report.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace MathInsight.Modules.Notification_Report.Tests;

public class GetLeaderboardQueryHandlerTests : IDisposable
{
    private readonly NotificationDbContext _db;
    private readonly Mock<ILeaderboardCacheService> _cache = new();

    public GetLeaderboardQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new NotificationDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private void SeedStudent(string accountId, string firstName, string lastName, int grade, decimal point)
    {
        _db.Accounts.Add(new AccountReadOnly { AccountId = accountId, FirstName = firstName, LastName = lastName });
        _db.CompetencyPoints.Add(new CompetencyPointReadOnly
        {
            CompetencyId = Guid.NewGuid().ToString(),
            StudentId = accountId,
            Grade = grade,
            Point = point
        });
    }

    [Theory]
    [InlineData(9)]
    [InlineData(13)]
    public async Task Handle_InvalidGrade_ReturnsFailure(int grade)
    {
        var handler = new GetLeaderboardQueryHandler(_db, _cache.Object);

        var result = await handler.Handle(new GetLeaderboardQuery(grade), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(NotificationErrors.LeaderboardGradeInvalid, result.Error);
    }

    [Fact]
    public async Task Handle_CacheHit_ReturnsCachedValue_WithoutWritingCache()
    {
        var cached = new List<LeaderboardEntryDto> { new(1, "s1", "An Nguyen", 10, 9.5m) };
        _cache.Setup(c => c.GetAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(cached);
        var handler = new GetLeaderboardQueryHandler(_db, _cache.Object);

        var result = await handler.Handle(new GetLeaderboardQuery(10), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(cached, result.Value);
        _cache.Verify(c => c.SetAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<LeaderboardEntryDto>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CacheMiss_RanksByPointDescending_AndWritesThroughToCache()
    {
        SeedStudent("s1", "An", "Nguyen", 10, 7.0m);
        SeedStudent("s2", "Binh", "Tran", 10, 9.0m);
        SeedStudent("s3", "Chi", "Le", 10, 8.0m);
        SeedStudent("s4", "Dung", "Pham", 11, 10.0m); // different grade — must be excluded
        await _db.SaveChangesAsync();

        _cache.Setup(c => c.GetAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<LeaderboardEntryDto>?)null);
        var handler = new GetLeaderboardQueryHandler(_db, _cache.Object);

        var result = await handler.Handle(new GetLeaderboardQuery(10), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var entries = result.Value!;
        Assert.Equal(3, entries.Count);
        Assert.Equal(new[] { "s2", "s3", "s1" }, entries.Select(e => e.StudentId));
        Assert.Equal(new[] { 1, 2, 3 }, entries.Select(e => e.Rank));
        Assert.Equal("Binh Tran", entries[0].StudentName);

        _cache.Verify(c => c.SetAsync(10, It.Is<IReadOnlyList<LeaderboardEntryDto>>(e => e.Count == 3), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    public async Task Handle_BoundaryValidGrades_AreAllAccepted(int grade)
    {
        _cache.Setup(c => c.GetAsync(grade, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<LeaderboardEntryDto>?)null);
        var handler = new GetLeaderboardQueryHandler(_db, _cache.Object);

        var result = await handler.Handle(new GetLeaderboardQuery(grade), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(NotificationErrors.LeaderboardGradeInvalid, result.Error);
    }

    [Fact]
    public async Task Handle_TopParameter_LimitsResultOnCacheMiss()
    {
        for (var i = 0; i < 10; i++)
        {
            SeedStudent($"s{i}", "S", i.ToString(), 10, i);
        }
        await _db.SaveChangesAsync();
        _cache.Setup(c => c.GetAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<LeaderboardEntryDto>?)null);
        var handler = new GetLeaderboardQueryHandler(_db, _cache.Object);

        var result = await handler.Handle(new GetLeaderboardQuery(10, Top: 3), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Count);
        Assert.Equal(new[] { 9m, 8m, 7m }, result.Value.Select(e => e.Point));
    }
}
