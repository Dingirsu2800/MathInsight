using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MathInsight.Modules.Grading_Analytics.Services;

/// <summary>
/// ChatbotService (UC-51): Sends question + student answer to Google Gemini API
/// and returns a step-by-step explanation. Response is NOT persisted (BR-21).
///
/// Resilience:
///   - 10-second request timeout (configured on HttpClient via DI)
///   - Polly circuit breaker: 3 consecutive failures = open for 30s (configured in DI)
///
/// Rate limiting (A2 — MVP):
///   - In-memory: 1 request per (studentId, sessionId) pair.
///   - No Redis for MVP — Constitution §IV prohibits it unless spec-backed.
///   - TTL-based cleanup to prevent unbounded memory growth.
/// </summary>
public class ChatbotService : IChatbotService
{
    private readonly HttpClient _httpClient;
    private readonly ChatbotOptions _options;
    private readonly ILogger<ChatbotService> _logger;

    // In-memory rate limiter: (studentId, sessionId) → timestamp of the request.
    // TTL-based eviction: entries older than 1 hour are cleaned up on each access.
    private static readonly ConcurrentDictionary<(string StudentId, string SessionId), DateTime> _rateLimitStore = new();

    // Cleanup threshold: evict entries older than this duration.
    private static readonly TimeSpan RateLimitTtl = TimeSpan.FromHours(1);
    private static DateTime _lastCleanup = DateTime.UtcNow;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(10);

    private const string SystemPrompt =
        "You are a math tutor. Explain the solution step-by-step in clear natural language. " +
        "Use simple Unicode/plain-text math notation where needed; do not require technical markup syntax.";

    public ChatbotService(
        HttpClient httpClient,
        IOptions<ChatbotOptions> options,
        ILogger<ChatbotService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> AskAsync(
        string questionContent,
        string studentAnswer,
        string studentId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        // ── A2: In-memory rate limiting ──────────────────────────────────────
        var key = (studentId, sessionId);
        EvictExpiredEntries();

        if (!_rateLimitStore.TryAdd(key, DateTime.UtcNow))
        {
            _logger.LogWarning(
                "Chatbot rate limit hit for Student={StudentId}, Session={SessionId}",
                studentId, sessionId);
            throw new ChatbotRateLimitException(studentId, sessionId);
        }

        // ── Build Gemini API request ─────────────────────────────────────────
        var userMessage = $"Question:\n{questionContent}\n\nStudent's answer:\n{studentAnswer}";

        var requestBody = new GeminiRequest
        {
            SystemInstruction = new GeminiSystemInstruction
            {
                Parts = [new GeminiPart { Text = SystemPrompt }]
            },
            Contents =
            [
                new GeminiContent
                {
                    Role = "user",
                    Parts = [new GeminiPart { Text = userMessage }]
                }
            ],
            GenerationConfig = new GeminiGenerationConfig
            {
                MaxOutputTokens = 2048,
                Temperature = 0.3
            }
        };

        // ── POST to Gemini API ───────────────────────────────────────────────
        // Timeout and circuit breaker are handled by HttpClient pipeline (Polly in DI).
        var apiUrl = $"v1beta/models/{_options.Model}:generateContent?key={_options.ApiKey}";

        _logger.LogInformation(
            "Chatbot request for Student={StudentId}, Session={SessionId}",
            studentId, sessionId);

        var response = await _httpClient.PostAsJsonAsync(apiUrl, requestBody, cancellationToken);
        response.EnsureSuccessStatusCode();

        var geminiResponse = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: cancellationToken);

        var explanation = geminiResponse?.Candidates?.FirstOrDefault()
            ?.Content?.Parts?.FirstOrDefault()?.Text;

        if (string.IsNullOrWhiteSpace(explanation))
        {
            _logger.LogWarning("Gemini returned empty response for Session={SessionId}", sessionId);
            return "Unable to generate explanation at this time. Please try again later.";
        }

        _logger.LogInformation(
            "Chatbot response received for Student={StudentId}, Session={SessionId} ({Length} chars)",
            studentId, sessionId, explanation.Length);

        return explanation;
    }

    /// <summary>
    /// Periodically removes expired entries from the rate limit store to prevent unbounded memory growth.
    /// </summary>
    private static void EvictExpiredEntries()
    {
        if (DateTime.UtcNow - _lastCleanup < CleanupInterval) return;

        _lastCleanup = DateTime.UtcNow;
        var cutoff = DateTime.UtcNow - RateLimitTtl;

        foreach (var kvp in _rateLimitStore)
        {
            if (kvp.Value < cutoff)
            {
                _rateLimitStore.TryRemove(kvp.Key, out _);
            }
        }
    }
}

/// <summary>
/// Thrown when a student exceeds the chatbot rate limit (1 request per session).
/// </summary>
public class ChatbotRateLimitException : Exception
{
    public string StudentId { get; }
    public string SessionId { get; }

    public ChatbotRateLimitException(string studentId, string sessionId)
        : base($"Chatbot rate limit exceeded for student {studentId} in session {sessionId}")
    {
        StudentId = studentId;
        SessionId = sessionId;
    }
}

/// <summary>
/// Configuration options for the chatbot service, bound from "Chatbot" config section.
/// </summary>
public class ChatbotOptions
{
    public const string SectionName = "Chatbot";

    /// <summary>
    /// Gemini API key. Injected via environment variable or configuration.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gemini model name. Defaults to gemini-2.0-flash.
    /// </summary>
    public string Model { get; set; } = "gemini-2.0-flash";

    /// <summary>
    /// Base URL for the Gemini API.
    /// </summary>
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/";
}

#region Gemini API DTOs (internal)

internal class GeminiRequest
{
    [JsonPropertyName("system_instruction")]
    public GeminiSystemInstruction? SystemInstruction { get; set; }

    [JsonPropertyName("contents")]
    public List<GeminiContent> Contents { get; set; } = [];

    [JsonPropertyName("generationConfig")]
    public GeminiGenerationConfig? GenerationConfig { get; set; }
}

internal class GeminiSystemInstruction
{
    [JsonPropertyName("parts")]
    public List<GeminiPart> Parts { get; set; } = [];
}

internal class GeminiContent
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("parts")]
    public List<GeminiPart> Parts { get; set; } = [];
}

internal class GeminiPart
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

internal class GeminiGenerationConfig
{
    [JsonPropertyName("maxOutputTokens")]
    public int MaxOutputTokens { get; set; }

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }
}

internal class GeminiResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate>? Candidates { get; set; }
}

internal class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; set; }
}

#endregion
