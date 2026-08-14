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

    /// <summary>
    /// Seeds one StudyStreak row. <paramref name="lastActivity"/> is nullable so the "row exists but
    /// was never dated" state can be reached. The change tracker is cleared afterwards so the
    /// handler re-reads the row exactly like a fresh request would.
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

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

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

    [Fact]
    public async Task LastActivityExactlyTwoDaysAgo_IsBroken()
    {
        // The first day on the inactive side of the today/yesterday window — the boundary the
        // three-days-ago test steps over.
        var twoDaysAgo = Today.AddDays(-2);
        await SeedAsync(current: 8, longest: 8, lastActivity: twoDaysAgo);

        var result = await _handler.Handle(new GetStreakQuery(StudentId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.False(dto.IsActive);
        Assert.Equal(0, dto.CurrentStreak);
        Assert.Equal(8, dto.LongestStreak);
        Assert.Equal(twoDaysAgo, dto.LastActivityDate);
    }

    [Fact]
    public async Task NullLastActivityDate_IsBroken_LongestPreserved()
    {
        // Row exists but has no date (legacy/imported): neither comparison matches, so the display
        // rule must treat it as lapsed rather than throwing or reporting active.
        await SeedAsync(current: 5, longest: 6, lastActivity: null);

        var result = await _handler.Handle(new GetStreakQuery(StudentId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.False(dto.IsActive);
        Assert.Equal(0, dto.CurrentStreak);
        Assert.Equal(6, dto.LongestStreak);
        Assert.Null(dto.LastActivityDate);
    }

    [Fact]
    public async Task FutureLastActivityDate_IsBroken()
    {
        // Clock skew / bad data: a date ahead of today is neither today nor yesterday. Documents
        // that the rule does not treat "not in the past" as active.
        var tomorrow = Today.AddDays(1);
        await SeedAsync(current: 4, longest: 4, lastActivity: tomorrow);

        var dto = (await _handler.Handle(new GetStreakQuery(StudentId), CancellationToken.None)).Value!;

        Assert.False(dto.IsActive);
        Assert.Equal(0, dto.CurrentStreak);
        Assert.Equal(4, dto.LongestStreak);
    }

    [Fact]
    public async Task OnlyOtherStudentHasRow_ReturnsZeroInactive()
    {
        // The query filters on the caller's id; another student's row must not stand in for it.
        await SeedAsync(current: 9, longest: 9, lastActivity: Today, studentId: "student-B");

        var dto = (await _handler.Handle(new GetStreakQuery(StudentId), CancellationToken.None)).Value!;

        Assert.False(dto.IsActive);
        Assert.Equal(0, dto.CurrentStreak);
        Assert.Equal(0, dto.LongestStreak);
        Assert.Null(dto.LastActivityDate);
    }

    [Fact]
    public async Task MultipleStudents_ReturnsOnlyCallersRow()
    {
        await SeedAsync(current: 2, longest: 3, lastActivity: Today);
        await SeedAsync(current: 9, longest: 9, lastActivity: Today, studentId: "student-B");

        var dto = (await _handler.Handle(new GetStreakQuery(StudentId), CancellationToken.None)).Value!;

        Assert.True(dto.IsActive);
        Assert.Equal(2, dto.CurrentStreak);   // never student-B's 9
        Assert.Equal(3, dto.LongestStreak);
    }

    [Fact]
    public async Task EmptyStudentId_ReturnsZeroInactive_WithoutLeakingAnotherRow()
    {
        // Defence in depth: the controller rejects a missing identity, but the handler itself must
        // still degrade to an empty streak rather than throwing or matching an arbitrary row.
        await SeedAsync(current: 7, longest: 7, lastActivity: Today);

        var result = await _handler.Handle(new GetStreakQuery(string.Empty), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal(0, dto.CurrentStreak);
        Assert.Equal(0, dto.LongestStreak);
        Assert.False(dto.IsActive);
    }

    [Fact]
    public async Task ActiveStreak_QueryDoesNotTrackOrMutate()
    {
        // AsNoTracking proof on the active path: the broken path already asserts the stored row is
        // untouched, but a read must also leave nothing tracked for a later SaveChanges to flush.
        await SeedAsync(current: 5, longest: 7, lastActivity: Today);

        await _handler.Handle(new GetStreakQuery(StudentId), CancellationToken.None);

        Assert.Empty(_db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task BrokenStreak_RepeatedQueries_AreIdempotent()
    {
        // Guards against a display rule that "helpfully" writes the zero back: querying twice must
        // return the same thing and leave CurrentStreak intact for StreakService to act on later.
        var fourDaysAgo = Today.AddDays(-4);
        await SeedAsync(current: 6, longest: 10, lastActivity: fourDaysAgo);

        var first = (await _handler.Handle(new GetStreakQuery(StudentId), CancellationToken.None)).Value!;
        var second = (await _handler.Handle(new GetStreakQuery(StudentId), CancellationToken.None)).Value!;

        Assert.Equal(first, second);   // record equality
        Assert.Equal(0, second.CurrentStreak);
        Assert.Equal(10, second.LongestStreak);

        var stored = await _db.StudyStreaks.AsNoTracking().FirstAsync(s => s.StudentId == StudentId);
        Assert.Equal(6, stored.CurrentStreak);
        Assert.Equal(fourDaysAgo, stored.LastActivityDate);
    }

    [Fact]
    public async Task CancelledToken_PropagatesCancellation()
    {
        await SeedAsync(current: 5, longest: 7, lastActivity: Today);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _handler.Handle(new GetStreakQuery(StudentId), cts.Token));
    }
}
