using MathInsight.Modules.Testing.Commands.AutoSave;
using MathInsight.Modules.Testing.Commands.StartSession;
using MathInsight.Modules.Testing.Contracts;
using MathInsight.Modules.Testing.Entities;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Testing.Tests;

public sealed class StartSessionAccessIntegrationTests
{
    [Fact]
    public async Task StartSession_PersonalTestOtherStudent_DeniedWithNoWrites()
    {
        await using var context = TestingInMemoryContext.Create();
        var db = context.Context;
        await TestDataSeeder.SeedActiveTestWithQuestions(db);

        var result = await StartAsync(db, TestDataSeeder.ActiveTestId, TestDataSeeder.OtherStudentId);

        AssertAccessDenied(result.Error?.Code);
        Assert.Empty(await db.TestSessions.ToListAsync());
        Assert.Empty(await db.TestAnswers.ToListAsync());
    }

    [Fact]
    public async Task StartSession_SharedCorrectGrade_SucceedsForMultipleStudents()
    {
        await using var context = TestingInMemoryContext.Create();
        var db = context.Context;
        await TestDataSeeder.SeedSharedBlueprintExam(db);

        var first = await StartAsync(db, TestDataSeeder.SharedTestId, TestDataSeeder.StudentId);
        var second = await StartAsync(db, TestDataSeeder.SharedTestId, TestDataSeeder.OtherStudentId);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, await db.TestSessions.CountAsync());
        Assert.Equal(2, await db.TestAnswers.CountAsync());
    }

    [Fact]
    public async Task StartSession_SharedWrongGrade_Denied()
    {
        await using var context = TestingInMemoryContext.Create();
        var db = context.Context;
        await TestDataSeeder.SeedSharedBlueprintExam(db, blueprintGrade: 11);

        var result = await StartAsync(db, TestDataSeeder.SharedTestId, TestDataSeeder.StudentId);

        AssertAccessDenied(result.Error?.Code);
        Assert.Empty(await db.TestSessions.ToListAsync());
    }

    [Fact]
    public async Task StartSession_SharedDeactivatedBlueprint_Denied()
    {
        await using var context = TestingInMemoryContext.Create();
        var db = context.Context;
        await TestDataSeeder.SeedSharedBlueprintExam(db, blueprintStatus: "Deactivated");

        var result = await StartAsync(db, TestDataSeeder.SharedTestId, TestDataSeeder.StudentId);

        AssertAccessDenied(result.Error?.Code);
        Assert.Empty(await db.TestSessions.ToListAsync());
    }

    [Fact]
    public async Task StartSession_NullOwnerUnsupportedMode_Denied()
    {
        await using var context = TestingInMemoryContext.Create();
        var db = context.Context;
        await TestDataSeeder.SeedSharedBlueprintExam(db, testMode: "AdaptivePractice");

        var result = await StartAsync(db, TestDataSeeder.SharedTestId, TestDataSeeder.StudentId);

        AssertAccessDenied(result.Error?.Code);
        Assert.Empty(await db.TestSessions.ToListAsync());
    }

    [Theory]
    [InlineData("BlueprintExam", "Exam")]
    [InlineData("Diagnostic", "Exam")]
    [InlineData("MockTest", "Exam")]
    [InlineData("AdaptivePractice", "Practice")]
    [InlineData("TopicPractice", "Practice")]
    [InlineData("Practice", "Practice")]
    public async Task StartSession_ValidTestMode_MapsToExpectedTestFormat(string testMode, string expectedFormat)
    {
        await using var context = TestingInMemoryContext.Create();
        var db = context.Context;
        await TestDataSeeder.SeedActiveTestWithQuestions(db);

        var test = await db.Tests.SingleAsync(item => item.TestId == TestDataSeeder.ActiveTestId);
        test.TestMode = testMode;
        await db.SaveChangesAsync();

        var result = await StartAsync(db, TestDataSeeder.ActiveTestId, TestDataSeeder.StudentId);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedFormat, result.Value!.TestFormat);
    }

    [Fact]
    public async Task StartSession_UnknownTestMode_ThrowsInvalidOperationException()
    {
        await using var context = TestingInMemoryContext.Create();
        var db = context.Context;
        await TestDataSeeder.SeedActiveTestWithQuestions(db);

        var test = await db.Tests.SingleAsync(item => item.TestId == TestDataSeeder.ActiveTestId);
        test.TestMode = "InvalidUnknownMode";
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => StartAsync(db, TestDataSeeder.ActiveTestId, TestDataSeeder.StudentId));
    }

    [Fact]
    public async Task StartSession_UnusableStudent_Denied()
    {
        await using var context = TestingInMemoryContext.Create();
        var db = context.Context;
        await TestDataSeeder.SeedActiveTestWithQuestions(db);
        var student = await db.Students.SingleAsync(item => item.StudentId == TestDataSeeder.StudentId);
        student.CurrentGrade = null;
        await db.SaveChangesAsync();

        var result = await StartAsync(db, TestDataSeeder.ActiveTestId, TestDataSeeder.StudentId);

        AssertAccessDenied(result.Error?.Code);
        Assert.Empty(await db.TestSessions.ToListAsync());
    }

    [Fact]
    public async Task StartSession_MissingStudent_Denied()
    {
        await using var context = TestingInMemoryContext.Create();
        var db = context.Context;
        await TestDataSeeder.SeedActiveTestWithQuestions(db);
        var student = await db.Students.SingleAsync(item => item.StudentId == TestDataSeeder.StudentId);
        db.Students.Remove(student);
        await db.SaveChangesAsync();

        var result = await StartAsync(db, TestDataSeeder.ActiveTestId, TestDataSeeder.StudentId);

        AssertAccessDenied(result.Error?.Code);
        Assert.Empty(await db.TestSessions.ToListAsync());
    }

    [Fact]
    public async Task ExistingSession_RemainsUsableAfterTestArchived()
    {
        await using var context = TestingInMemoryContext.Create();
        var db = context.Context;
        await TestDataSeeder.SeedActiveTestWithQuestions(db);
        var started = await StartAsync(db, TestDataSeeder.ActiveTestId, TestDataSeeder.StudentId);
        Assert.True(started.IsSuccess);

        var test = await db.Tests.SingleAsync(item => item.TestId == TestDataSeeder.ActiveTestId);
        test.TestStatus = "Archived";
        await db.SaveChangesAsync();

        var autoSave = await new AutoSaveCommandHandler(db).Handle(
            new AutoSaveCommand(
                started.Value!.SessionId,
                TestDataSeeder.StudentId,
                [new AutoSaveAnswerDto(TestDataSeeder.Question1Id, TestDataSeeder.Answer1Id, null, 5, null, null)]),
            CancellationToken.None);

        Assert.True(autoSave.IsSuccess);
        var session = await db.TestSessions.SingleAsync(item => item.SessionId == started.Value.SessionId);
        Assert.Equal("InProgress", session.Status);
    }

    private static Task<MathInsight.Shared.Results.Result<MathInsight.Modules.Testing.Contracts.StartSessionResponse>> StartAsync(
        Persistence.TestingDbContext db,
        string testId,
        string studentId)
        => new StartSessionCommandHandler(db).Handle(
            new StartSessionCommand(testId, studentId),
            CancellationToken.None);

    private static void AssertAccessDenied(string? errorCode)
        => Assert.Equal("TESTING_TEST_ACCESS_DENIED", errorCode);
}
