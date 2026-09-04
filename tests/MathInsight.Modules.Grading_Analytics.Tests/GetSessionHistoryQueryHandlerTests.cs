using MathInsight.Modules.Grading_Analytics.Persistence;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;
using MathInsight.Modules.Grading_Analytics.Queries.GetSessionHistory;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Grading_Analytics.Tests;

public sealed class GetSessionHistoryQueryHandlerTests
{
    private static GradingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GradingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("D"))
            .Options;
        return new GradingDbContext(options);
    }

    [Fact]
    public async Task Handle_DateFilter_IncludesSessionsOnEndDate()
    {
        await using var db = CreateDbContext();
        const string studentId = "student-date-test";

        db.Tests.AddRange(
            new Test { TestId = "test-1", TestName = "Test 1" },
            new Test { TestId = "test-2", TestName = "Test 2" },
            new Test { TestId = "test-3", TestName = "Test 3" },
            new Test { TestId = "test-4", TestName = "Test 4" }
        );

        // Session on 2026-09-01
        db.TestSessions.Add(new TestSession
        {
            SessionId = "session-1",
            TestId = "test-1",
            StudentId = studentId,
            TestFormat = "Practice",
            Status = "Graded",
            Score = 8.0m,
            StartTime = new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 9, 1, 9, 30, 0, DateTimeKind.Utc),
        });

        // Session on 2026-09-03 afternoon (the critical case that was previously excluded)
        db.TestSessions.Add(new TestSession
        {
            SessionId = "session-2",
            TestId = "test-2",
            StudentId = studentId,
            TestFormat = "Exam",
            Status = "Graded",
            Score = 7.5m,
            StartTime = new DateTime(2026, 9, 3, 14, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 9, 3, 15, 30, 0, DateTimeKind.Utc),
        });

        // Session on 2026-09-04 (outside range)
        db.TestSessions.Add(new TestSession
        {
            SessionId = "session-3",
            TestId = "test-3",
            StudentId = studentId,
            TestFormat = "Practice",
            Status = "Graded",
            Score = 9.0m,
            StartTime = new DateTime(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 9, 4, 10, 45, 0, DateTimeKind.Utc),
        });

        // Session on 2026-08-31 (before range)
        db.TestSessions.Add(new TestSession
        {
            SessionId = "session-4",
            TestId = "test-4",
            StudentId = studentId,
            TestFormat = "Practice",
            Status = "Graded",
            Score = 6.0m,
            StartTime = new DateTime(2026, 8, 31, 20, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 8, 31, 20, 30, 0, DateTimeKind.Utc),
        });

        await db.SaveChangesAsync();

        var handler = new GetSessionHistoryQueryHandler(db);

        // Filter from 2026-09-01 (00:00:00) to 2026-09-03 (00:00:00 - date only)
        var query = new GetSessionHistoryQuery(
            StudentId: studentId,
            Page: 1,
            PageSize: 20,
            TestFormat: null,
            FromDate: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            ToDate: new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc)
        );

        var result = await handler.Handle(query, CancellationToken.None);

        // Both session-1 (01/09) and session-2 (03/09) must be included!
        Assert.Equal(2, result.TotalCount);
        Assert.Contains(result.Items, s => s.SessionId == "session-1");
        Assert.Contains(result.Items, s => s.SessionId == "session-2");
        Assert.DoesNotContain(result.Items, s => s.SessionId == "session-3");
        Assert.DoesNotContain(result.Items, s => s.SessionId == "session-4");
    }

    [Fact]
    public async Task Handle_FallbackToStartTime_WhenEndTimeIsNull()
    {
        await using var db = CreateDbContext();
        const string studentId = "student-fallback-test";

        db.Tests.Add(new Test { TestId = "test-fallback", TestName = "Fallback Test" });

        db.TestSessions.Add(new TestSession
        {
            SessionId = "session-null-end",
            TestId = "test-fallback",
            StudentId = studentId,
            TestFormat = "Practice",
            Status = "Graded",
            Score = 8.0m,
            StartTime = new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc),
            EndTime = null,
        });

        await db.SaveChangesAsync();

        var handler = new GetSessionHistoryQueryHandler(db);
        var query = new GetSessionHistoryQuery(
            StudentId: studentId,
            Page: 1,
            PageSize: 20,
            TestFormat: null,
            FromDate: new DateTime(2026, 9, 1),
            ToDate: new DateTime(2026, 9, 3)
        );

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("session-null-end", result.Items[0].SessionId);
    }
}
