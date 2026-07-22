using MathInsight.Modules.Gamification.Entities;
using MathInsight.Modules.Gamification.Persistence;
using MathInsight.Modules.Gamification.Queries.GetStreak;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MathInsight.Modules.Gamification.Tests;

/// <summary>
/// Unit tests for GetStreakQueryHandler (UC-81). Verifies the display rule: a streak counts as
/// active only if the last activity was today or yesterday; a longer gap displays 0 WITHOUT
/// mutating the stored row (read-only query). Uses EF Core InMemory, mirroring StreakServiceTests.
/// </summary>
public class GetStreakQueryHandlerTests : IDisposable
{
    private const string StudentId = "student-A";

    private readonly GamificationDbContext _db;
    private readonly GetStreakQueryHandler _handler;

    public GetStreakQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<GamificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new GamificationDbContext(options);
        _handler = new GetStreakQueryHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task SeedAsync(int current, int longest, DateOnly lastActivity)
    {
        _db.StudyStreaks.Add(new StudyStreak
        {
            StreakId = Guid.NewGuid().ToString(),
            StudentId = StudentId,
            CurrentStreak = current,
            LongestStreak = longest,
            LastActivityDate = lastActivity
        });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task NoRow_ReturnsZeroInactive()
    {
        var result = await _handler.Handle(new GetStreakQuery(StudentId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal(0, dto.CurrentStreak);
        Assert.Equal(0, dto.LongestStreak);
        Assert.Null(dto.LastActivityDate);
        Assert.False(dto.IsActive);
    }

    [Fact]
    public async Task LastActivityToday_IsActive_CurrentAsStored()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedAsync(current: 5, longest: 7, lastActivity: today);

        var dto = (await _handler.Handle(new GetStreakQuery(StudentId), CancellationToken.None)).Value!;

        Assert.True(dto.IsActive);
        Assert.Equal(5, dto.CurrentStreak);
        Assert.Equal(7, dto.LongestStreak);
    }

    [Fact]
    public async Task LastActivityYesterday_IsActive_CurrentAsStored()
    {
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        await SeedAsync(current: 3, longest: 9, lastActivity: yesterday);

        var dto = (await _handler.Handle(new GetStreakQuery(StudentId), CancellationToken.None)).Value!;

        Assert.True(dto.IsActive);
        Assert.Equal(3, dto.CurrentStreak);
        Assert.Equal(9, dto.LongestStreak);
    }

    [Fact]
    public async Task LastActivityThreeDaysAgo_BrokenInResponse_StoredRowUnchanged()
    {
        var threeDaysAgo = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3);
        await SeedAsync(current: 4, longest: 6, lastActivity: threeDaysAgo);

        var dto = (await _handler.Handle(new GetStreakQuery(StudentId), CancellationToken.None)).Value!;

        Assert.False(dto.IsActive);
        Assert.Equal(0, dto.CurrentStreak);          // display 0 when broken
        Assert.Equal(6, dto.LongestStreak);           // longest always as stored
        Assert.Equal(threeDaysAgo, dto.LastActivityDate);

        // Read-only: the persisted row must still hold its original CurrentStreak.
        var stored = await _db.StudyStreaks.AsNoTracking()
            .FirstAsync(s => s.StudentId == StudentId);
        Assert.Equal(4, stored.CurrentStreak);
        Assert.Equal(threeDaysAgo, stored.LastActivityDate);
    }
}
