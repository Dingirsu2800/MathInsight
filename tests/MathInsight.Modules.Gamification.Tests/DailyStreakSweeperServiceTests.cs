using MathInsight.Modules.Gamification.BackgroundJobs;
using MathInsight.Modules.Gamification.Entities;
using MathInsight.Modules.Gamification.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MathInsight.Modules.Gamification.Tests;

public class DailyStreakSweeperServiceTests : IDisposable
{
    private readonly GamificationDbContext _db;
    private readonly DailyStreakSweeperService _sweeper;
    private readonly IServiceScopeFactory _scopeFactory;

    public DailyStreakSweeperServiceTests()
    {
        var options = new DbContextOptionsBuilder<GamificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new GamificationDbContext(options);

        var services = new ServiceCollection();
        services.AddSingleton(_db);
        var provider = services.BuildServiceProvider();

        var logger = new Mock<ILogger<DailyStreakSweeperService>>();
        
        _sweeper = new DailyStreakSweeperService(provider, logger.Object);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task SweepExpiredStreaksAsync_InactiveForOver24Hours_ResetsToZero()
    {
        // Arrange: Inactive for 2 days
        _db.StudyStreaks.Add(new StudyStreak
        {
            StreakId = Guid.NewGuid().ToString(),
            StudentId = "student-1",
            CurrentStreak = 5,
            LongestStreak = 10,
            LastActivityDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2))
        });
        await _db.SaveChangesAsync();

        // Act
        await _sweeper.SweepExpiredStreaksAsync(CancellationToken.None);

        // Assert
        var streak = await _db.StudyStreaks.FirstOrDefaultAsync(s => s.StudentId == "student-1");
        Assert.NotNull(streak);
        Assert.Equal(0, streak.CurrentStreak);
        Assert.Equal(10, streak.LongestStreak); // Longest preserved
    }

    [Fact]
    public async Task SweepExpiredStreaksAsync_ActiveYesterday_Bypasses()
    {
        // Arrange: Active yesterday
        _db.StudyStreaks.Add(new StudyStreak
        {
            StreakId = Guid.NewGuid().ToString(),
            StudentId = "student-2",
            CurrentStreak = 5,
            LongestStreak = 10,
            LastActivityDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))
        });
        await _db.SaveChangesAsync();

        // Act
        await _sweeper.SweepExpiredStreaksAsync(CancellationToken.None);

        // Assert
        var streak = await _db.StudyStreaks.FirstOrDefaultAsync(s => s.StudentId == "student-2");
        Assert.NotNull(streak);
        Assert.Equal(5, streak.CurrentStreak); // Preserved
    }
}
