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

    /// <summary>
    /// Puts an existing StudyStreak row in place so a test can start from a mid-life state
    /// (including <c>LastActivityDate == null</c>, which no service path can produce itself).
    /// The change tracker is cleared so the service re-reads the row like a fresh request would.
    /// </summary>
    private async Task SeedAsync(int current, int longest, DateOnly? lastActivity, string studentId = StudentId)
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
        _db.ChangeTracker.Clear();
    }

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

    [Fact]
    public async Task ViewLecture_ZeroSeconds_DoesNotQualify_NoRow()
    {
        await _service.UpdateStreakAsync(StudentId, ActivityType.VIEW_LECTURE, 0, Day(1));

        Assert.Null(await LoadAsync());
    }

    [Fact]
    public async Task ViewLecture_NegativeSeconds_DoesNotQualify_NoRow()
    {
        // A malformed/negative duration must never be read as "qualifying".
        await _service.UpdateStreakAsync(StudentId, ActivityType.VIEW_LECTURE, -1, Day(1));

        Assert.Null(await LoadAsync());
    }

    [Fact]
    public async Task UnrecognisedActivityType_DoesNotQualify_NoRow()
    {
        // Default arm of the qualification switch: an out-of-range enum value (e.g. a new activity
        // type added upstream without updating BR-39) must be rejected, not silently counted.
        await _service.UpdateStreakAsync(StudentId, (ActivityType)99, 100_000, Day(1));

        Assert.Null(await LoadAsync());
    }

    [Fact]
    public async Task Practice_FreshStudent_AssignsStreakIdAndStudentId()
    {
        await _service.UpdateStreakAsync(StudentId, ActivityType.PRACTICE, 0, Day(1));

        var streak = await LoadAsync();
        Assert.NotNull(streak);
        Assert.Equal(StudentId, streak!.StudentId);
        Assert.True(Guid.TryParse(streak.StreakId, out var id) && id != Guid.Empty);
    }

    [Fact]
    public async Task NoPriorActivityDate_TreatedAsGap_ResetsToOne()
    {
        // Row exists but LastActivityDate is NULL (legacy/imported row): neither "same day" nor
        // "yesterday" matches, so the else-branch starts a fresh streak at 1 and longest survives.
        await SeedAsync(current: 5, longest: 5, lastActivity: null);

        await _service.UpdateStreakAsync(StudentId, ActivityType.PRACTICE, 0, Day(10));

        var streak = await LoadAsync();
        Assert.Equal(1, streak!.CurrentStreak);
        Assert.Equal(5, streak.LongestStreak);
        Assert.Equal(Day(10), streak.LastActivityDate);
    }

    [Fact]
    public async Task NonQualifyingActivity_ExistingRow_LeavesRowUntouched()
    {
        await SeedAsync(current: 3, longest: 4, lastActivity: Day(1));

        await _service.UpdateStreakAsync(StudentId, ActivityType.DOWNLOAD_MATERIAL, 100_000, Day(2));

        var streak = await LoadAsync();
        Assert.Equal(3, streak!.CurrentStreak);
        Assert.Equal(4, streak.LongestStreak);
        Assert.Equal(Day(1), streak.LastActivityDate);   // date not advanced either
    }

    [Fact]
    public async Task SameDayRepeat_MidStreak_DoesNotDoubleCount()
    {
        // Idempotency must hold at any streak length, not only on day one.
        await _service.UpdateStreakAsync(StudentId, ActivityType.PRACTICE, 0, Day(1));
        await _service.UpdateStreakAsync(StudentId, ActivityType.PRACTICE, 0, Day(2));
        await _service.UpdateStreakAsync(StudentId, ActivityType.VIEW_LECTURE, 600, Day(2));

        var streak = await LoadAsync();
        Assert.Equal(2, streak!.CurrentStreak);
        Assert.Equal(2, streak.LongestStreak);
        Assert.Equal(Day(2), streak.LastActivityDate);
    }

    [Fact]
    public async Task ConsecutiveDays_AcrossMonthBoundary_Increments()
    {
        // 31 Jul → 1 Aug is still "yesterday"; the +1 day comparison must not be day-of-month based.
        await _service.UpdateStreakAsync(StudentId, ActivityType.PRACTICE, 0, new DateOnly(2026, 7, 31));
        await _service.UpdateStreakAsync(StudentId, ActivityType.PRACTICE, 0, new DateOnly(2026, 8, 1));

        var streak = await LoadAsync();
        Assert.Equal(2, streak!.CurrentStreak);
        Assert.Equal(2, streak.LongestStreak);
        Assert.Equal(new DateOnly(2026, 8, 1), streak.LastActivityDate);
    }

    [Fact]
    public async Task ConsecutiveDays_AcrossYearBoundary_Increments()
    {
        await _service.UpdateStreakAsync(StudentId, ActivityType.EXAM, 0, new DateOnly(2026, 12, 31));
        await _service.UpdateStreakAsync(StudentId, ActivityType.EXAM, 0, new DateOnly(2027, 1, 1));

        var streak = await LoadAsync();
        Assert.Equal(2, streak!.CurrentStreak);
        Assert.Equal(new DateOnly(2027, 1, 1), streak.LastActivityDate);
    }

    [Fact]
    public async Task BackdatedActivity_OlderThanLastActivity_ResetsToOne()
    {
        // Out-of-order/replayed event: the date is neither today nor yesterday, so it falls into the
        // reset branch. Documents the current contract — longest is never damaged by a late arrival.
        await SeedAsync(current: 3, longest: 3, lastActivity: Day(10));

        await _service.UpdateStreakAsync(StudentId, ActivityType.PRACTICE, 0, Day(5));

        var streak = await LoadAsync();
        Assert.Equal(1, streak!.CurrentStreak);
        Assert.Equal(3, streak.LongestStreak);
        Assert.Equal(Day(5), streak.LastActivityDate);
    }

    [Fact]
    public async Task RebuildAfterBreak_LongestUnchangedUntilStrictlyExceeded()
    {
        // BR-42 equality boundary: while rebuilding, current == longest must NOT rewrite longest;
        // only current > longest does.
        await SeedAsync(current: 1, longest: 3, lastActivity: Day(6));

        await _service.UpdateStreakAsync(StudentId, ActivityType.PRACTICE, 0, Day(7));
        await _service.UpdateStreakAsync(StudentId, ActivityType.PRACTICE, 0, Day(8));

        var atEqual = await LoadAsync();
        Assert.Equal(3, atEqual!.CurrentStreak);
        Assert.Equal(3, atEqual.LongestStreak);   // equal → not rewritten

        await _service.UpdateStreakAsync(StudentId, ActivityType.PRACTICE, 0, Day(9));

        var afterExceed = await LoadAsync();
        Assert.Equal(4, afterExceed!.CurrentStreak);
        Assert.Equal(4, afterExceed.LongestStreak);   // strictly greater → grown
    }

    [Fact]
    public async Task Streaks_AreScopedPerStudent()
    {
        const string otherStudent = "student-B";
        await SeedAsync(current: 7, longest: 9, lastActivity: Day(1), studentId: otherStudent);

        await _service.UpdateStreakAsync(StudentId, ActivityType.PRACTICE, 0, Day(1));

        var mine = await LoadAsync();
        Assert.Equal(1, mine!.CurrentStreak);

        var theirs = await LoadAsync(otherStudent);
        Assert.Equal(7, theirs!.CurrentStreak);   // untouched
        Assert.Equal(9, theirs.LongestStreak);
    }

    [Fact]
    public async Task NonQualifyingActivity_CancelledToken_ReturnsWithoutThrowing()
    {
        // The BR-39 guard returns before any awaited work, so cancellation is never observed.
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await _service.UpdateStreakAsync(
            StudentId, ActivityType.DOWNLOAD_MATERIAL, 100_000, Day(1), cts.Token);

        Assert.Null(await LoadAsync());
    }

    [Fact]
    public async Task QualifyingActivity_CancelledToken_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.UpdateStreakAsync(StudentId, ActivityType.PRACTICE, 0, Day(1), cts.Token));

        Assert.Null(await LoadAsync());   // nothing persisted
    }
}
