using System.Security.Claims;
using System.Text.Json;
using MathInsight.Modules.QuestionBank.Commands.ReportQuestion;
using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Modules.Testing.Controllers;
using MathInsight.Modules.Testing.Persistence;
using MathInsight.Shared.Questions;
using MathInsight.Shared.Results;
using MathInsight.Shared.Events;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace MathInsight.Modules.Testing.Tests;

public sealed class TestingControllerTests
{
    [Fact]
    public async Task GetSession_RendersImmutableSnapshotWithoutCorrectAnswerFlags()
    {
        await using var db = CreateDbContext();
        SeedTest(db);
        await db.SaveChangesAsync();
        var controller = CreateController(db, new Mock<IMediator>());

        var result = await controller.GetSession("session_01", CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<TestSessionViewResponse>(response.Value);
        var question = Assert.Single(payload.Questions);
        Assert.Equal("version_01", question.QuestionVersionId);
        Assert.Equal(2.5m, question.MaxPoints);
        Assert.Equal(["Correct", "Wrong"], question.AnswerOptions.Select(item => item.AnswerContent));
        Assert.DoesNotContain("IsCorrect", JsonSerializer.Serialize(question), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitSession_RejectsAnswerOutsideImmutableVersionBeforeWriting()
    {
        await using var db = CreateDbContext();
        SeedTest(db);
        await db.SaveChangesAsync();
        var mediator = new Mock<IMediator>();
        var controller = CreateController(db, mediator);
        var request = new SubmitSessionRequest(
        [
            new SubmittedAnswerRequest(
                "question_01",
                "answer_from_another_version",
                [],
                null,
                [],
                20)
        ]);

        var result = await controller.SubmitSession("session_01", request, CancellationToken.None);

        var response = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("ANSWER_NOT_IN_TEST_VERSION", JsonSerializer.Serialize(response.Value));
        Assert.Empty(await db.TestAnswers.ToListAsync());
        mediator.Verify(
            item => item.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReportSessionQuestion_DerivesVersionFromTestQuestion()
    {
        await using var db = CreateDbContext();
        SeedTest(db);
        await db.SaveChangesAsync();
        ReportQuestionCommand? sentCommand = null;
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(item => item.Send(It.IsAny<ReportQuestionCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<ReportQuestionResponse>>, CancellationToken>((command, _) =>
                sentCommand = Assert.IsType<ReportQuestionCommand>(command))
            .ReturnsAsync(Result<ReportQuestionResponse>.Success(new ReportQuestionResponse(
                "report_01",
                "question_01",
                "Student",
                "The answer key is incorrect.",
                "Pending",
                DateTime.UtcNow,
                "Approved",
                true,
                "session_01",
                "version_01")));
        var controller = CreateController(db, mediator);

        var result = await controller.ReportSessionQuestion(
            "session_01",
            "question_01",
            new ReportQuestionRequest { ReportReason = "The answer key is incorrect." },
            CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, response.StatusCode);
        Assert.NotNull(sentCommand);
        Assert.Equal("student_01", sentCommand!.ReporterAccountId);
        Assert.Equal("session_01", sentCommand.SessionId);
        Assert.Equal("version_01", sentCommand.QuestionVersionId);
    }

    [Fact]
    public async Task SubmitSession_WhenGradingFails_RemovesSavedAnswersSoStudentCanRetry()
    {
        await using var db = CreateDbContext();
        SeedTest(db);
        await db.SaveChangesAsync();
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(item => item.Publish(It.IsAny<TestSubmittedEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Grading unavailable"));
        var controller = CreateController(db, mediator);
        var request = new SubmitSessionRequest(
        [
            new SubmittedAnswerRequest(
                "question_01",
                "answer_correct",
                [],
                null,
                [],
                20)
        ]);

        var result = await controller.SubmitSession("session_01", request, CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, response.StatusCode);
        Assert.Empty(await db.TestAnswers.ToListAsync());
        var session = await db.TestSessions.SingleAsync();
        Assert.Equal("InProgress", session.Status);
        Assert.Null(session.EndTime);
    }

    private static TestingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TestingDbContext>()
            .UseInMemoryDatabase($"testing-{Guid.NewGuid():D}")
            .Options;
        return new TestingDbContext(options);
    }

    private static TestingController CreateController(TestingDbContext db, Mock<IMediator> mediator)
    {
        var controller = new TestingController(db, mediator.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "student_01"),
                    new Claim(ClaimTypes.Role, "Student")
                ], "TestAuth"))
            }
        };
        return controller;
    }

    private static void SeedTest(TestingDbContext db)
    {
        var snapshot = new QuestionSnapshotV2(
            "question_01",
            "SINGLE_CHOICE",
            "DIFF-MEDIUM",
            12,
            1m,
            [new QuestionTopicSnapshot("TOPIC-G12-DERIVAPP", true)],
            [
                new QuestionAnswerSnapshot("answer_correct", "Correct", true),
                new QuestionAnswerSnapshot("answer_wrong", "Wrong", false)
            ],
            []);

        db.Tests.Add(new TestReadModel
        {
            TestId = "test_01",
            TestName = "Blueprint exam",
            TestMode = "BlueprintExam",
            TestStatus = "Active",
            DurationMinutes = 90,
            TotalQuestions = 1,
            MaxScore = 10m
        });
        db.TestQuestions.Add(new TestQuestionReadModel
        {
            TestId = "test_01",
            QuestionId = "question_01",
            QuestionOrder = 1,
            QuestionVersionId = "version_01",
            MaxPointsSnapshot = 2.5m,
            ScoringRuleSnapshot = "AllOrNothing"
        });
        db.QuestionVersions.Add(new QuestionVersionReadModel
        {
            VersionId = "version_01",
            QuestionId = "question_01",
            QuestionContent = "Immutable question",
            QuestionAnswer = "Solution",
            AnswersSnapshot = JsonSerializer.Serialize(snapshot),
            SnapshotSchemaVersion = 2
        });
        db.TestSessions.Add(new TestSession
        {
            SessionId = "session_01",
            TestId = "test_01",
            StudentId = "student_01",
            TestFormat = "Exam",
            Status = "InProgress",
            StartTime = DateTime.UtcNow,
            TotalQuestion = 1
        });
    }
}
