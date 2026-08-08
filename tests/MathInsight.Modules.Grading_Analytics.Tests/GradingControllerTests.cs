using System.Security.Claims;
using MathInsight.Modules.Grading_Analytics.Controllers;
using MathInsight.Modules.Grading_Analytics.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace MathInsight.Modules.Grading_Analytics.Tests;

/// <summary>
/// TC-INT-GradingController-001..005
/// Integration tests for GradingController (POST /api/v1/chatbot/assist, UC-51).
/// Uses Mock&lt;IChatbotService&gt; to isolate HTTP status code mapping logic.
/// </summary>
public sealed class GradingControllerTests
{
    private const string StudentId = "student-ctrl-001";
    private const string SessionId = "session-ctrl-001";

    // ── Helper ──────────────────────────────────────────────────────────────

    private static GradingController CreateController(
        IChatbotService chatbotService,
        bool withStudentClaim = true)
    {
        var controller = new GradingController(
            chatbotService,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GradingController>.Instance);

        var claims = withStudentClaim
            ? new[] { new Claim(ClaimTypes.NameIdentifier, StudentId) }
            : Array.Empty<Claim>();

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
            }
        };
        return controller;
    }

    private static ChatbotAssistRequest MakeRequest() => new()
    {
        SessionId  = SessionId,
        QuestionId = "question-001",
        QuestionContent = "What is 2+2?",
        StudentAnswer   = "4"
    };

    // ── TC-INT-GradingController-001 ─────────────────────────────────────────

    /// <summary>
    /// TC-INT-GradingController-001: UC-51 — Happy path.
    /// AskAsync returns explanation → controller returns 200 OK with ChatbotAssistResponse.
    /// </summary>
    [Fact]
    public async Task AskChatbot_HappyPath_Returns200WithExplanation()
    {
        var chatbot = new Mock<IChatbotService>();
        chatbot
            .Setup(s => s.AskAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                StudentId, SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Step 1: 2+2=4 because...");

        var controller = CreateController(chatbot.Object);

        var result = await controller.AskChatbot(MakeRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ChatbotAssistResponse>(ok.Value);
        Assert.Contains("Step 1", response.Explanation);
    }

    // ── TC-INT-GradingController-002 ─────────────────────────────────────────

    /// <summary>
    /// TC-INT-GradingController-002: UC-51 — Missing student identity claim.
    /// No NameIdentifier claim → controller returns 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task AskChatbot_MissingStudentClaim_Returns401()
    {
        var chatbot = new Mock<IChatbotService>();
        var controller = CreateController(chatbot.Object, withStudentClaim: false);

        var result = await controller.AskChatbot(MakeRequest(), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        chatbot.Verify(s => s.AskAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── TC-INT-GradingController-003 ─────────────────────────────────────────

    /// <summary>
    /// TC-INT-GradingController-003: UC-51 — Rate limit hit (same session used twice).
    /// ChatbotRateLimitException → controller returns 429 Too Many Requests.
    /// </summary>
    [Fact]
    public async Task AskChatbot_RateLimitException_Returns429()
    {
        var chatbot = new Mock<IChatbotService>();
        chatbot
            .Setup(s => s.AskAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                StudentId, SessionId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ChatbotRateLimitException(StudentId, SessionId));

        var controller = CreateController(chatbot.Object);

        var result = await controller.AskChatbot(MakeRequest(), CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, statusResult.StatusCode);
    }

    // ── TC-INT-GradingController-004 ─────────────────────────────────────────

    /// <summary>
    /// TC-INT-GradingController-004: UC-51 — Gemini API timeout.
    /// TaskCanceledException(inner: TimeoutException) → controller returns 503.
    /// </summary>
    [Fact]
    public async Task AskChatbot_Timeout_Returns503()
    {
        var chatbot = new Mock<IChatbotService>();
        chatbot
            .Setup(s => s.AskAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                StudentId, SessionId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("timeout", new TimeoutException()));

        var controller = CreateController(chatbot.Object);

        var result = await controller.AskChatbot(MakeRequest(), CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusResult.StatusCode);
    }

    // ── TC-INT-GradingController-005 ─────────────────────────────────────────

    /// <summary>
    /// TC-INT-GradingController-005: UC-51 — Gemini API HTTP error (e.g. 500).
    /// HttpRequestException → controller returns 503 Service Unavailable.
    /// </summary>
    [Fact]
    public async Task AskChatbot_HttpRequestException_Returns503()
    {
        var chatbot = new Mock<IChatbotService>();
        chatbot
            .Setup(s => s.AskAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                StudentId, SessionId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Gemini 500"));

        var controller = CreateController(chatbot.Object);

        var result = await controller.AskChatbot(MakeRequest(), CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusResult.StatusCode);
    }
}
