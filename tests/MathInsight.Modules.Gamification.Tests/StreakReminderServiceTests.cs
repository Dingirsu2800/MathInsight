using MathInsight.Modules.Gamification.Entities;
using MathInsight.Modules.Gamification.Persistence;
using MathInsight.Modules.Gamification.Services;
using MathInsight.Shared.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace MathInsight.Modules.Gamification.Tests;

/// <summary>
/// Unit tests for StreakReminderService (BR-45). Verifies that a reminder is published only for
/// students with no qualifying activity today. Uses EF Core InMemory and a captured IMediator;
/// the BackgroundService timer/scheduling is intentionally not tested here.
/// </summary>
public class StreakReminderServiceTests : IDisposable
{
    private readonly GamificationDbContext _db;

    public StreakReminderServiceTests()
    {
        var options = new DbContextOptionsBuilder<GamificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new GamificationDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private async Task SeedAsync(string studentId, int current, int longest, DateOnly? lastActivity)
    {
        _db.StudyStreaks.Add(new StudyStreak
        {
            StreakId = Guid.NewGuid().ToString(),
            StudentId = studentId,
            CurrentStreak = current,
            LongestStreak = longest,
            LastActivityDate = lastActivity
        });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task SendReminders_PublishesOnlyForInactiveStudents_AndReturnsCount()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await SeedAsync("active-today", current: 5, longest: 5, lastActivity: today);            // NOT reminded
        await SeedAsync("inactive-yesterday", current: 3, longest: 4, lastActivity: today.AddDays(-1)); // reminded
        await SeedAsync("never-active", current: 0, longest: 0, lastActivity: null);              // reminded

        // Capture every published StreakReminderEvent.
        var published = new List<StreakReminderEvent>();
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Publish(It.IsAny<StreakReminderEvent>(), It.IsAny<CancellationToken>()))
            .Callback<StreakReminderEvent, CancellationToken>((evt, _) => published.Add(evt))
            .Returns(Task.CompletedTask);

        var service = new StreakReminderService(_db, mediator.Object);

        var count = await service.SendRemindersAsync(today, CancellationToken.None);

        // Exactly the two inactive students, and the returned count agrees.
        Assert.Equal(2, count);
        Assert.Equal(2, published.Count);

        var remindedIds = published.Select(e => e.StudentId).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "inactive-yesterday", "never-active" }, remindedIds);
        Assert.DoesNotContain(published, e => e.StudentId == "active-today");

        // Publish was invoked exactly twice for StreakReminderEvent.
        mediator.Verify(
            m => m.Publish(It.IsAny<StreakReminderEvent>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        // Sanity: the event carries the streak snapshot the notification needs.
        var neverActive = Assert.Single(published, e => e.StudentId == "never-active");
        Assert.Null(neverActive.LastActivityDate);
        Assert.Equal(0, neverActive.CurrentStreak);
    }

    [Fact]
    public async Task SendReminders_AllActive_PublishesNothing()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedAsync("a", current: 1, longest: 1, lastActivity: today);
        await SeedAsync("b", current: 2, longest: 2, lastActivity: today);

        var mediator = new Mock<IMediator>();
        var service = new StreakReminderService(_db, mediator.Object);

        var count = await service.SendRemindersAsync(today, CancellationToken.None);

        Assert.Equal(0, count);
        mediator.Verify(
            m => m.Publish(It.IsAny<StreakReminderEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
