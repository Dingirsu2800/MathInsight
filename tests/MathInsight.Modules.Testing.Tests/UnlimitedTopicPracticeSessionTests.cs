using MathInsight.Modules.Testing.Commands.AutoSave;
using MathInsight.Modules.Testing.Commands.ForceSubmitSession;
using MathInsight.Modules.Testing.Commands.RecordIncident;
using MathInsight.Modules.Testing.Commands.StartSession;
using MathInsight.Modules.Testing.Commands.SubmitSession;
using MathInsight.Modules.Testing.Commands.TimeoutSubmitSession;
using MathInsight.Modules.Testing.Contracts;
using MathInsight.Modules.Testing.Queries.GetSessionContent;
using MathInsight.Shared.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace MathInsight.Modules.Testing.Tests;

public sealed class UnlimitedTopicPracticeSessionTests
{
    [Fact]
    public async Task UnlimitedPractice_StartResumeAndAutoSave_ReturnNullableTimeContract()
    {
        await using var fixture = TestingInMemoryContext.Create();
        await SeedUnlimitedTopicPracticeAsync(fixture.Context);

        var start = await new StartSessionCommandHandler(fixture.Context).Handle(
            new StartSessionCommand(TestDataSeeder.ActiveTestId, TestDataSeeder.StudentId),
            CancellationToken.None);

        Assert.True(start.IsSuccess);
        Assert.Equal("Practice", start.Value!.TestFormat);
        Assert.False(start.Value.HasTimeLimit);
        Assert.Null(start.Value.RemainingSeconds);

        var session = await fixture.Context.TestSessions.SingleAsync();
        session.StartTime = DateTime.UtcNow.AddDays(-7);
        await fixture.Context.SaveChangesAsync();

        var save = await new AutoSaveCommandHandler(fixture.Context).Handle(
            new AutoSaveCommand(
                session.SessionId,
                TestDataSeeder.StudentId,
                [new AutoSaveAnswerDto(TestDataSeeder.Question1Id, TestDataSeeder.Answer1Id, null, 12, null, null)]),
            CancellationToken.None);
        var resume = await new GetSessionContentQueryHandler(fixture.Context).Handle(
            new GetSessionContentQuery(session.SessionId, TestDataSeeder.StudentId),
            CancellationToken.None);

        Assert.True(save.IsSuccess);
        Assert.False(save.Value!.HasTimeLimit);
        Assert.Null(save.Value.RemainingSeconds);
        Assert.True(save.Value.ElapsedSeconds >= 7 * 24 * 60 * 60);
        Assert.True(resume.IsSuccess);
        Assert.Equal("Practice", resume.Value!.TestFormat);
        Assert.False(resume.Value.HasTimeLimit);
        Assert.Null(resume.Value.RemainingSeconds);
        Assert.True(resume.Value.ElapsedSeconds >= 7 * 24 * 60 * 60);
    }

    [Fact]
    public async Task UnlimitedPractice_TimeoutIsRejected_ButManualSubmitStillGrades()
    {
        await using var fixture = TestingInMemoryContext.Create();
        await SeedUnlimitedTopicPracticeAsync(fixture.Context);
        var start = await new StartSessionCommandHandler(fixture.Context).Handle(
            new StartSessionCommand(TestDataSeeder.ActiveTestId, TestDataSeeder.StudentId),
            CancellationToken.None);
        var session = await fixture.Context.TestSessions.SingleAsync();
        session.StartTime = DateTime.UtcNow.AddDays(-7);
        await fixture.Context.SaveChangesAsync();
        var timeoutMediator = new Mock<IMediator>();

        var timeout = await new TimeoutSubmitSessionCommandHandler(fixture.Context, timeoutMediator.Object).Handle(
            new TimeoutSubmitSessionCommand(start.Value!.SessionId, TestDataSeeder.StudentId),
            CancellationToken.None);
        var submit = await new SubmitSessionCommandHandler(
            fixture.Context,
            CreateGradingMediator(fixture.Context)).Handle(
                new SubmitSessionCommand(start.Value.SessionId, TestDataSeeder.StudentId),
                CancellationToken.None);

        Assert.True(timeout.IsFailure);
        Assert.Equal("TESTING_TEST_HAS_NO_TIME_LIMIT", timeout.Error!.Code);
        timeoutMediator.Verify(
            mediator => mediator.Send(It.IsAny<ForceSubmitSessionCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.True(submit.IsSuccess);
        Assert.Equal("Graded", submit.Value!.Status);
        Assert.Equal("StudentSubmit", submit.Value.SubmissionType);
    }

    [Fact]
    public async Task UnlimitedPractice_FiveIncidents_DoNotForceSubmit()
    {
        await using var fixture = TestingInMemoryContext.Create();
        await SeedUnlimitedTopicPracticeAsync(fixture.Context);
        var start = await new StartSessionCommandHandler(fixture.Context).Handle(
            new StartSessionCommand(TestDataSeeder.ActiveTestId, TestDataSeeder.StudentId),
            CancellationToken.None);
        var mediator = new Mock<IMediator>();
        var handler = new RecordIncidentCommandHandler(fixture.Context, mediator.Object);

        for (var index = 0; index < 5; index++)
        {
            var result = await handler.Handle(
                new RecordIncidentCommand(start.Value!.SessionId, TestDataSeeder.StudentId, "TAB_SWITCH"),
                CancellationToken.None);
            Assert.True(result.IsSuccess);
            Assert.False(result.Value!.ForceSubmitted);
        }

        mediator.Verify(
            item => item.Send(It.IsAny<ForceSubmitSessionCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Equal("InProgress", (await fixture.Context.TestSessions.SingleAsync()).Status);
    }

    private static async Task SeedUnlimitedTopicPracticeAsync(Persistence.TestingDbContext context)
    {
        await TestDataSeeder.SeedActiveTestWithQuestions(context);
        var test = await context.Tests.SingleAsync(item => item.TestId == TestDataSeeder.ActiveTestId);
        test.TestMode = "TopicPractice";
        test.DurationMinutes = 0;
        await context.SaveChangesAsync();
    }

    private static IMediator CreateGradingMediator(Persistence.TestingDbContext context)
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(item => item.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(async (INotification notification, CancellationToken cancellationToken) =>
            {
                if (notification is not TestSubmittedEvent submitted)
                    return;

                var session = await context.TestSessions.SingleAsync(
                    item => item.SessionId == submitted.SessionId,
                    cancellationToken);
                session.Status = "Graded";
                session.SubmissionType = submitted.SubmissionType;
                session.EndTime = submitted.SubmittedTime;
                await context.SaveChangesAsync(cancellationToken);
            });
        return mediator.Object;
    }
}
