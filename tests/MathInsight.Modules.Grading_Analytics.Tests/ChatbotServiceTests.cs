using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MathInsight.Modules.Grading_Analytics.Services;

namespace MathInsight.Modules.Grading_Analytics.Tests;

/// <summary>
/// Tests for ChatbotService (UC-51):
///   - Happy path: returns explanation
///   - Timeout: throws TaskCanceledException (controller maps to 503)
///   - Rate limiting: second call same (studentId, sessionId) throws ChatbotRateLimitException
/// </summary>
public class ChatbotServiceTests
{
    private readonly ILogger<ChatbotService> _logger =
        new Mock<ILogger<ChatbotService>>().Object;

    private ChatbotService CreateService(HttpClient httpClient)
    {
        var options = Options.Create(new ChatbotOptions
        {
            ApiKey = "test-api-key",
            Model = "gemini-2.0-flash",
            BaseUrl = "https://generativelanguage.googleapis.com/"
        });

        return new ChatbotService(httpClient, options, _logger);
    }

    private static HttpClient CreateMockHttpClient(HttpMessageHandler handler, TimeSpan? timeout = null)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/"),
            Timeout = timeout ?? TimeSpan.FromSeconds(10)
        };
        return client;
    }

    [Fact]
    public async Task AskAsync_HappyPath_ReturnsExplanation()
    {
        // Arrange
        var geminiResponse = new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        role = "model",
                        parts = new[] { new { text = "Step 1: The answer is 42 because..." } }
                    }
                }
            }
        };

        var handler = new FakeHttpMessageHandler(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(geminiResponse));

        var httpClient = CreateMockHttpClient(handler);
        var service = CreateService(httpClient);

        var studentId = Guid.NewGuid().ToString("D");
        var sessionId = Guid.NewGuid().ToString("D");

        // Act
        var result = await service.AskAsync("What is 6 * 7?", "42", studentId, sessionId);

        // Assert
        Assert.Contains("42", result);
        Assert.Contains("Step 1", result);
    }

    [Fact]
    public async Task AskAsync_TimeoutAfter10Seconds_ThrowsTaskCanceled()
    {
        // Arrange: handler that delays beyond timeout
        var handler = new DelayedHttpMessageHandler(TimeSpan.FromSeconds(15));

        var httpClient = CreateMockHttpClient(handler, timeout: TimeSpan.FromMilliseconds(100));
        var service = CreateService(httpClient);

        var studentId = Guid.NewGuid().ToString("D");
        var sessionId = Guid.NewGuid().ToString("D");

        // Act & Assert: TaskCanceledException is thrown (which maps to 503 in controller)
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => service.AskAsync("What is 6 * 7?", "42", studentId, sessionId));
    }

    [Fact]
    public async Task AskAsync_SecondCallSameSession_ThrowsChatbotRateLimitException()
    {
        // Arrange
        var geminiResponse = new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        role = "model",
                        parts = new[] { new { text = "Explanation text" } }
                    }
                }
            }
        };

        var handler = new FakeHttpMessageHandler(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(geminiResponse));

        var httpClient = CreateMockHttpClient(handler);
        var service = CreateService(httpClient);

        var studentId = Guid.NewGuid().ToString("D");
        var sessionId = Guid.NewGuid().ToString("D");

        // Act: first call succeeds
        await service.AskAsync("Question", "Answer", studentId, sessionId);

        // Assert: second call with SAME (studentId, sessionId) throws rate limit
        var ex = await Assert.ThrowsAsync<ChatbotRateLimitException>(
            () => service.AskAsync("Question2", "Answer2", studentId, sessionId));

        Assert.Equal(studentId, ex.StudentId);
        Assert.Equal(sessionId, ex.SessionId);
    }

    [Fact]
    public async Task AskAsync_DifferentSessions_BothSucceed()
    {
        // Arrange
        var geminiResponse = new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        role = "model",
                        parts = new[] { new { text = "Explanation" } }
                    }
                }
            }
        };

        var handler = new FakeHttpMessageHandler(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(geminiResponse));

        var httpClient = CreateMockHttpClient(handler);
        var service = CreateService(httpClient);

        var studentId = Guid.NewGuid().ToString("D");
        var sessionId1 = Guid.NewGuid().ToString("D");
        var sessionId2 = Guid.NewGuid().ToString("D");

        // Act & Assert: different sessions = no rate limiting
        var result1 = await service.AskAsync("Q1", "A1", studentId, sessionId1);
        var result2 = await service.AskAsync("Q2", "A2", studentId, sessionId2);

        Assert.NotEmpty(result1);
        Assert.NotEmpty(result2);
    }

    [Fact]
    public async Task AskAsync_ApiError_ThrowsHttpRequestException()
    {
        // Arrange: handler that returns 500
        var handler = new FakeHttpMessageHandler(
            HttpStatusCode.InternalServerError,
            "{\"error\": \"Server error\"}");

        var httpClient = CreateMockHttpClient(handler);
        var service = CreateService(httpClient);

        var studentId = Guid.NewGuid().ToString("D");
        var sessionId = Guid.NewGuid().ToString("D");

        // Act & Assert: HttpRequestException maps to 503 in controller
        await Assert.ThrowsAsync<HttpRequestException>(
            () => service.AskAsync("Q", "A", studentId, sessionId));
    }
}

/// <summary>
/// Fake HttpMessageHandler that returns a predefined response.
/// </summary>
internal class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _content;

    public FakeHttpMessageHandler(HttpStatusCode statusCode, string content)
    {
        _statusCode = statusCode;
        _content = content;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_content, System.Text.Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}

/// <summary>
/// Fake HttpMessageHandler that delays beyond a timeout to simulate network timeout.
/// </summary>
internal class DelayedHttpMessageHandler : HttpMessageHandler
{
    private readonly TimeSpan _delay;

    public DelayedHttpMessageHandler(TimeSpan delay)
    {
        _delay = delay;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Task.Delay(_delay, cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.OK);
    }
}
