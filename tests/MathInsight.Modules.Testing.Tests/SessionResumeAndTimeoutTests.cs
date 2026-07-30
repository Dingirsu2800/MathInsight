using MathInsight.Modules.Testing.Commands.AutoSave;
using MathInsight.Modules.Testing.Commands.ForceSubmitSession;
using MathInsight.Modules.Testing.Commands.StartSession;
using MathInsight.Modules.Testing.Commands.SubmitSession;
using MathInsight.Modules.Testing.Commands.TimeoutSubmitSession;
using MathInsight.Modules.Testing.Contracts;
using MathInsight.Modules.Testing.Queries.GetSessionContent;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace MathInsight.Modules.Testing.Tests;

public sealed class SessionResumeAndTimeoutTests
{
    [Fact]
    public async Task GetSessionContent_AfterAutoSave_ReturnsSavedOptionsAndParts()
    {
        await using var context = TestingInMemoryContext.Create();
        var db = context.Context;
        await TestDataSeeder.SeedActiveTestWithQuestions(db);
        var start = await new StartSessionCommandHandler(db).Handle(
            new StartSessionCommand(TestDataSeeder.ActiveTestId, TestDataSeeder.StudentId),
            CancellationToken.None);
        var sessionId = start.Value!.SessionId;
        var updates = new List<AutoSaveAnswerDto>
        {
            new(
                TestDataSeeder.Question3Id,
                null,
                null,
                25,
                [new AutoSaveOptionDto("opt-a"), new AutoSaveOptionDto("opt-b")],
                null),
            new(
                TestDataSeeder.Question4Id,
                null,
                null,
                40,
                null,
                [
                    new AutoSavePartDto("part-1", true, null, null),
                    new AutoSavePartDto("part-2", null, "answer text", null)
                ])
        };
        var saved = await new AutoSaveCommandHandler(db).Handle(
            new AutoSaveCommand(sessionId, TestDataSeeder.StudentId, updates),
            CancellationToken.None);

        var result = await new GetSessionContentQueryHandler(db).Handle(
            new GetSessionContentQuery(sessionId, TestDataSeeder.StudentId),
            CancellationToken.None);

        Assert.True(saved.IsSuccess);
        Assert.True(result.IsSuccess);
        Assert.Equal("Practice", result.Value!.TestFormat);
        Assert.True(result.Value!.HasTimeLimit);
        Assert.InRange(result.Value.RemainingSeconds!.Value, 1, 3600);
        var multipleChoice = Assert.Single(
            result.Value.SavedAnswers,
            answer => answer.QuestionId == TestDataSeeder.Question3Id);
        Assert.Equal(["opt-a", "opt-b"], multipleChoice.SelectedOptions.Select(option => option.AnswerId));
        Assert.Equal(25, multipleChoice.TimeSpent);
        var composite = Assert.Single(
            result.Value.SavedAnswers,
            answer => answer.QuestionId == TestDataSeeder.Question4Id);
        Assert.Equal(2, composite.Parts.Count);
        Assert.True(composite.Parts.Single(part => part.PartId == "part-1").BooleanAnswer);
        Assert.Equal("answer text", composite.Parts.Single(part => part.PartId == "part-2").TextAnswer);
    }

    [Fact]
    public async Task TimeoutSubmit_BeforeExpiry_ReturnsSessionNotExpiredAndDoesNotForceSubmit()
    {
        await using var context = TestingInMemoryContext.Create();
        var db = context.Context;
        await TestDataSeeder.SeedActiveTestWithQuestions(db);
        var start = await new StartSessionCommandHandler(db).Handle(
            new StartSessionCommand(TestDataSeeder.ActiveTestId, TestDataSeeder.StudentId),
            CancellationToken.None);
        var mediator = new Mock<IMediator>();

        var result = await new TimeoutSubmitSessionCommandHandler(db, mediator.Object).Handle(
            new TimeoutSubmitSessionCommand(start.Value!.SessionId, TestDataSeeder.StudentId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("TESTING_SESSION_NOT_EXPIRED", result.Error!.Code);
        mediator.Verify(
            item => item.Send(It.IsAny<ForceSubmitSessionCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TimeoutSubmit_AfterExpiry_UsesTimeoutSubmissionType()
    {
        await using var context = TestingInMemoryContext.Create();
        var db = context.Context;
        await TestDataSeeder.SeedActiveTestWithQuestions(db);
        var start = await new StartSessionCommandHandler(db).Handle(
            new StartSessionCommand(TestDataSeeder.ActiveTestId, TestDataSeeder.StudentId),
            CancellationToken.None);
        var session = await db.TestSessions.SingleAsync(item => item.SessionId == start.Value!.SessionId);
        session.StartTime = DateTime.UtcNow.AddMinutes(-61);
        await db.SaveChangesAsync();

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(item => item.Send(
                It.Is<ForceSubmitSessionCommand>(command =>
                    command.SessionId == session.SessionId &&
                    command.SubmissionType == "TimeoutSubmit"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SubmitSessionResponse>.Success(new(
                session.SessionId,
                "InProgress",
                "TimeoutSubmit",
                5,
                null)));

        var result = await new TimeoutSubmitSessionCommandHandler(db, mediator.Object).Handle(
            new TimeoutSubmitSessionCommand(session.SessionId, TestDataSeeder.StudentId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("TimeoutSubmit", result.Value!.SubmissionType);
        mediator.VerifyAll();
    }

    [Fact]
    public async Task AutoSave_AfterExpiry_IsRejectedWithoutChangingAnswer()
    {
        await using var context = TestingInMemoryContext.Create();
        var db = context.Context;
        await TestDataSeeder.SeedActiveTestWithQuestions(db);
        var start = await new StartSessionCommandHandler(db).Handle(
            new StartSessionCommand(TestDataSeeder.ActiveTestId, TestDataSeeder.StudentId),
            CancellationToken.None);
        var session = await db.TestSessions.SingleAsync(item => item.SessionId == start.Value!.SessionId);
        session.StartTime = DateTime.UtcNow.AddMinutes(-61);
        await db.SaveChangesAsync();

        var result = await new AutoSaveCommandHandler(db).Handle(
            new AutoSaveCommand(
                session.SessionId,
                TestDataSeeder.StudentId,
                [new AutoSaveAnswerDto(
                    TestDataSeeder.Question1Id,
                    TestDataSeeder.Answer1Id,
                    null,
                    10,
                    null,
                    null)]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("TESTING_SESSION_EXPIRED", result.Error!.Code);
        var persisted = await db.TestAnswers.SingleAsync(answer =>
            answer.SessionId == session.SessionId &&
            answer.QuestionId == TestDataSeeder.Question1Id);
        Assert.Null(persisted.AnswerId);
    }

    [Fact]
    public async Task NormalSubmit_AfterExpiry_IsConvertedToTimeoutSubmit()
    {
        await using var context = TestingInMemoryContext.Create();
        var db = context.Context;
        await TestDataSeeder.SeedActiveTestWithQuestions(db);
        var start = await new StartSessionCommandHandler(db).Handle(
            new StartSessionCommand(TestDataSeeder.ActiveTestId, TestDataSeeder.StudentId),
            CancellationToken.None);
        var session = await db.TestSessions.SingleAsync(item => item.SessionId == start.Value!.SessionId);
        session.StartTime = DateTime.UtcNow.AddMinutes(-61);
        await db.SaveChangesAsync();
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(item => item.Send(
                It.Is<ForceSubmitSessionCommand>(command => command.SubmissionType == "TimeoutSubmit"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SubmitSessionResponse>.Success(new(
                session.SessionId,
                "InProgress",
                "TimeoutSubmit",
                5,
                null)));

        var result = await new SubmitSessionCommandHandler(db, mediator.Object).Handle(
            new SubmitSessionCommand(session.SessionId, TestDataSeeder.StudentId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("TimeoutSubmit", result.Value!.SubmissionType);
        mediator.VerifyAll();
    }
}
