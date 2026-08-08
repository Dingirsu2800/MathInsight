using System.Diagnostics;
using MathInsight.Modules.Grading_Analytics.Controllers;
using MathInsight.Modules.Grading_Analytics.Persistence;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;
using MathInsight.Modules.Grading_Analytics.Services;
using MathInsight.Modules.Grading_Analytics.Queries.GetSessionResult;
using MathInsight.Modules.Grading_Analytics.Queries.GetSessionHistory;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;

namespace MathInsight.Modules.Grading_Analytics.Tests;

/// <summary>
/// TC-SYS-GA-Flow-001..003
/// System-level flow tests for Grading_Analytics module.
/// Exercises Chatbot rate-limit flow and StudentGrading access-control flow
/// at controller level using Mock patterns.
/// </summary>
public sealed class GradingHttpFlowTests
{
    private const string StudentId  = "student-ga-flow-001";
    private const string SessionId  = "session-ga-flow-001";

    // ── TC-SYS-GA-Flow-001 ────────────────────────────────────────────────────

    /// <summary>
    /// TC-SYS-GA-Flow-001: UC-51 — Chatbot: first call succeeds, second call same session → 429.
    /// Validates the rate-limit-at-controller level behavior across two sequential requests.
    /// </summary>
    [Fact]
    public async Task ChatbotFlow_FirstCallSuccess_SecondCallSameSession_Returns429()
    {
        var chatbot = new Mock<IChatbotService>();
        chatbot
            .SetupSequence(s => s.AskAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                StudentId, SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Step 1: explanation")           // First call succeeds
            .ThrowsAsync(new ChatbotRateLimitException(StudentId, SessionId)); // Second call rate-limited

        var controller = CreateGradingController(chatbot.Object);

        // First call
        var request = new ChatbotAssistRequest
        {
            SessionId = SessionId, QuestionContent = "Q?", StudentAnswer = "A"
        };
        var first = await controller.AskChatbot(request, CancellationToken.None);
        Assert.IsType<OkObjectResult>(first);

        // Second call — same session
        var second = await controller.AskChatbot(request, CancellationToken.None);
        var statusResult = Assert.IsType<ObjectResult>(second);
        Assert.Equal(StatusCodes.Status429TooManyRequests, statusResult.StatusCode);
    }

    // ── TC-SYS-GA-Flow-002 ────────────────────────────────────────────────────

    /// <summary>
    /// TC-SYS-GA-Flow-002: UC-55 — GetSessionResult: owner gets 200, non-owner gets 403.
    /// Validates access-control is enforced consistently at controller level.
    /// </summary>
    [Fact]
    public async Task SessionResultFlow_OwnerGets200_NonOwnerGets403()
    {
        // Owner → handler returns DTO
        var ownerMediator = new Mock<IMediator>();
        ownerMediator
            .Setup(m => m.Send(It.IsAny<GetSessionResultQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionResultDto
            {
                SessionId     = SessionId,
                TestFormat    = "Exam",
                Status        = "Graded",
                Score         = 9.0m,
                NumCorrect    = 5,
                NumIncorrect  = 0,
                NumAbandoned  = 0,
                TotalQuestion = 5,
                GradeRevision = 1,
                Answers       = []
            });

        var ownerController = CreateStudentGradingController(ownerMediator.Object, StudentId);
        var ownerResult = await ownerController.GetSessionResult(SessionId, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(ownerResult);
        Assert.Equal(9.0m, ((SessionResultDto)ok.Value!).Score);

        // Non-owner → handler throws UnauthorizedAccessException
        var nonOwnerMediator = new Mock<IMediator>();
        nonOwnerMediator
            .Setup(m => m.Send(It.IsAny<GetSessionResultQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Not owner"));

        var nonOwnerController = CreateStudentGradingController(nonOwnerMediator.Object, "other-student");
        var nonOwnerResult = await nonOwnerController.GetSessionResult(SessionId, CancellationToken.None);
        var forbidden = Assert.IsType<ObjectResult>(nonOwnerResult);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    // ── TC-SYS-GA-Flow-003 ────────────────────────────────────────────────────

    /// <summary>
    /// TC-SYS-GA-Flow-003: UC-56 — GetHistory with testFormat filter → 200 with filtered result.
    /// Validates that query filter parameters are correctly propagated to the handler.
    /// </summary>
    [Fact]
    public async Task HistoryFlow_WithExamFilter_Returns200FilteredList()
    {
        var pagedResult = new PagedResult<SessionHistoryDto>
        {
            Items      =
            [
                new SessionHistoryDto { SessionId = "s1", TestFormat = "Exam", Status = "Graded" },
                new SessionHistoryDto { SessionId = "s2", TestFormat = "Exam", Status = "Graded" }
            ],
            TotalCount = 2,
            Page       = 1,
            PageSize   = 20
        };

        GetSessionHistoryQuery? capturedQuery = null;
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<GetSessionHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetSessionHistoryQuery q, CancellationToken _) =>
            {
                capturedQuery = q;
                return pagedResult;
            });

        var controller = CreateStudentGradingController(mediator.Object, StudentId);

        var result = await controller.GetHistory(
            page: 1, pageSize: 20,
            testFormat: "Exam",
            cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var paged = Assert.IsType<PagedResult<SessionHistoryDto>>(ok.Value);
        Assert.Equal(2, paged.TotalCount);
        Assert.All(paged.Items, item => Assert.Equal("Exam", item.TestFormat));

        // Verify filter was propagated
        Assert.NotNull(capturedQuery);
        Assert.Equal("Exam", capturedQuery!.TestFormat);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GradingController CreateGradingController(IChatbotService chatbot)
    {
        var controller = new GradingController(
            chatbot,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GradingController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, StudentId)], "Test"))
            }
        };
        return controller;
    }

    private static StudentGradingController CreateStudentGradingController(
        IMediator mediator, string studentId)
    {
        var controller = new StudentGradingController(mediator);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, studentId)], "Test"))
            }
        };
        return controller;
    }
}
