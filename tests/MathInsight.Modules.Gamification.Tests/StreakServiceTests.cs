using MathInsight.Modules.Gamification.Enums;
using MathInsight.Modules.Gamification.Entities;
using MathInsight.Modules.Gamification.Persistence;
using MathInsight.Modules.Gamification.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MathInsight.Modules.Gamification.Tests;

/// <summary>
/// Unit tests for StreakService (BR-39..BR-42). Uses EF Core InMemory to stand in for SQL Server
/// without a real connection, mirroring the Recommender test project's approach.
///
/// Note: the InMemory provider does not enforce the CK_StudyStreak_Values CHECK constraint; the
/// service's own ordering (longest updated after current) guarantees CurrentStreak &lt;= LongestStreak
/// before every save, which is what keeps the real DB constraint satisfied in production.
/// </summary>
public class StreakServiceTests : IDisposable
{
    private const string StudentId = "student-A";

    private readonly GamificationDbContext _db;
    private readonly StreakService _service;

    public StreakServiceTests()
    {
        var options = new DbContextOptionsBuilder<GamificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new GamificationDbContext(options);
        _service = new StreakService(_db);
    }

    public void Dispose() => _db.Dispose();

    private Task<StudyStreak?> LoadAsync(string studentId = StudentId) =>
        _db.StudyStreaks.AsNoTracking().FirstOrDefaultAsync(s => s.StudentId == studentId);

    private static DateOnly Day(int n) => new(2026, 7, n);

    [Fact]
    public async Task Practice_FreshStudent_CreatesRowAtOne()
    {
        await _service.UpdateStreakAsync(StudentId, ActivityType.PRACTICE, 0, Day(1));

        var streak = await LoadAsync();
        Assert.NotNull(streak);
        Assert.Equal(1, streak!.CurrentStreak);
        Assert.Equal(1, streak.LongestStreak);
        Assert.Equal(Day(1), streak.LastActivityDate);
    }

    [Fact]
    public async Task Exam_FreshStudent_Qualifies()
    {
        await _service.UpdateStreakAsync(StudentId, ActivityType.EXAM, 0, Day(1));

        var streak = await LoadAsync();
        Assert.NotNull(streak);
        Assert.Equal(1, streak!.CurrentStreak);
    }

    [Fact]
    public async Task ViewLecture_Exactly300Seconds_Qualifies()
    {
        await _service.UpdateStreakAsync(StudentId, ActivityType.VIEW_LECTURE, 300, Day(1));

        var streak = await LoadAsync();
        Assert.NotNull(streak);
        Assert.Equal(1, streak!.CurrentStreak);
    }

    [Fact]
    public async Task ViewLecture_360Seconds_Qualifies()
    {
        await _service.UpdateStreakAsync(StudentId, ActivityType.VIEW_LECTURE, 360, Day(1));

        Assert.NotNull(await LoadAsync());
    }

    [Fact]
    public async Task ViewLecture_299Seconds_DoesNotQualify_NoRow()
    {
        await _service.UpdateStreakAsync(StudentId, ActivityType.VIEW_LECTURE, 299, Day(1));

        Assert.Null(await LoadAsync());
    }

    [Fact]
    public async Task DownloadMaterial_NeverQualifies_NoRow()
    {
        await _service.UpdateStreakAsync(StudentId, ActivityType.DOWNLOAD_MATERIAL, 100_000, Day(1));

        Assert.Null(await LoadAsync());
    }

    [Fact]
    public async Task ConsecutiveDays_IncrementsToTwo()
    {
        await _service.UpdateStreakAsync(StudentId, ActivityType.PRACTICE, 0, Day(1));
        await _service.UpdateStreakAsync(StudentId, ActivityType.PRACTICE, 0, Day(2));

        var streak = await LoadAsync();
        Assert.Equal(2, streak!.CurrentStreak);
        Assert.Equal(2, streak.LongestStreak);
        Assert.Equal(Day(2), streak.LastActivityDate);
    }

    [Fact]
    public async Task SameDayRepeat_IsIdempotent_StaysAtOne()
    {
        await _service.UpdateStreakAsync(StudentId, ActivityType.PRACTICE, 0, Day(1));
        await _service.UpdateStreakAsync(StudentId, ActivityType.EXAM, 0, Day(1));

        var streak = await LoadAsync();
        Assert.Equal(1, streak!.CurrentStreak);
        Assert.Equal(1, streak.LongestStreak);
    }

    [Fact]
    public async Task GapOfTwoDays_ResetsToOne_LongestPreserved()
    {
        // Build a 2-day run (longest becomes 2), then skip a day.
        await _service.UpdateStreakAsync(StudentId, ActivityType.PRACTICE, 0, Day(1));
        await _service.UpdateStreakAsync(StudentId, ActivityType.PRACTICE, 0, Day(2));

        // Day 3 skipped; next activity on Day 4 → gap > 1 day.
        await _service.UpdateStreakAsync(StudentId, ActivityType.PRACTICE, 0, Day(4));

        var streak = await LoadAsync();
        Assert.Equal(1, streak!.CurrentStreak);   // reset to 1, not 0, not continued
        Assert.Equal(2, streak.LongestStreak);    // preserved
        Assert.Equal(Day(4), streak.LastActivityDate);
    }

    [Fact]
    public async Task StreakOfThree_ThenBreak_LongestStaysThree_CurrentBecomesOne()
    {
        await _service.UpdateStreakAsync(StudentId, ActivityType.PRACTICE, 0, Day(1));
        await _service.UpdateStreakAsync(StudentId, ActivityType.PRACTICE, 0, Day(2));
        await _service.UpdateStreakAsync(StudentId, ActivityType.PRACTICE, 0, Day(3));

        var mid = await LoadAsync();
        Assert.Equal(3, mid!.CurrentStreak);
        Assert.Equal(3, mid.LongestStreak);

        // Break: skip Day 4 and Day 5, resume on Day 6.
        await _service.UpdateStreakAsync(StudentId, ActivityType.PRACTICE, 0, Day(6));

        var after = await LoadAsync();
        Assert.Equal(1, after!.CurrentStreak);
        Assert.Equal(3, after.LongestStreak);
    }
}
