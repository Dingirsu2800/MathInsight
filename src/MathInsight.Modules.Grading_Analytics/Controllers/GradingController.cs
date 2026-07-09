using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MathInsight.Modules.Grading_Analytics.Services;

namespace MathInsight.Modules.Grading_Analytics.Controllers;

/// <summary>
/// REST endpoint for chatbot assistance (UC-51).
/// Only authenticated Students can access this endpoint.
/// </summary>
[ApiController]
[Route("api/v1/chatbot")]
[Authorize(Roles = "Student")]
public class GradingController : ControllerBase
{
    private readonly IChatbotService _chatbotService;
    private readonly ILogger<GradingController> _logger;

    public GradingController(
        IChatbotService chatbotService,
        ILogger<GradingController> logger)
    {
        _chatbotService = chatbotService;
        _logger = logger;
    }

    /// <summary>
    /// UC-51: Ask the AI chatbot for a step-by-step explanation.
    /// Accepts the session context and returns an explanation string.
    /// Rate limited: 1 request per student per session (in-memory, MVP).
    /// </summary>
    [HttpPost("assist")]
    public async Task<IActionResult> AskChatbot(
        [FromBody] ChatbotAssistRequest request,
        CancellationToken cancellationToken)
    {
        // Extract student ID from JWT claims
        var studentIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(studentIdClaim) || !Guid.TryParse(studentIdClaim, out var studentId))
        {
            return Unauthorized(new { error = "Invalid or missing student identity." });
        }

        try
        {
            var explanation = await _chatbotService.AskAsync(
                request.QuestionContent,
                request.StudentAnswer,
                studentId,
                request.SessionId,
                cancellationToken);

            return Ok(new ChatbotAssistResponse { Explanation = explanation });
        }
        catch (ChatbotRateLimitException)
        {
            _logger.LogWarning(
                "Rate limit hit: Student={StudentId}, Session={SessionId}",
                studentId, request.SessionId);

            return StatusCode(429, new { error = "You have already used the chatbot for this session." });
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning(
                "Chatbot request timed out: Student={StudentId}, Session={SessionId}",
                studentId, request.SessionId);

            return StatusCode(503, new { error = "The AI service is currently unavailable. Please try again later." });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "Chatbot API error: Student={StudentId}, Session={SessionId}",
                studentId, request.SessionId);

            return StatusCode(503, new { error = "The AI service encountered an error. Please try again later." });
        }
    }
}

/// <summary>
/// Request body for POST /api/v1/chatbot/assist.
/// </summary>
public record ChatbotAssistRequest
{
    /// <summary>The test session ID.</summary>
    public Guid SessionId { get; init; }

    /// <summary>The question ID (for audit/logging context).</summary>
    public Guid QuestionId { get; init; }

    /// <summary>The question text content.</summary>
    public string QuestionContent { get; init; } = string.Empty;

    /// <summary>The student's answer text.</summary>
    public string StudentAnswer { get; init; } = string.Empty;
}

/// <summary>
/// Response body for POST /api/v1/chatbot/assist.
/// </summary>
public record ChatbotAssistResponse
{
    /// <summary>Step-by-step explanation from the AI chatbot.</summary>
    public string Explanation { get; init; } = string.Empty;
}
