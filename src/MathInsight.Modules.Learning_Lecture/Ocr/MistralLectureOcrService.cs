using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MathInsight.Modules.Learning_Lecture.Configuration;
using MathInsight.Shared.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MathInsight.Modules.Learning_Lecture.Ocr;

public sealed class MistralLectureOcrService : ILectureOcrService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly MistralOcrOptions _options;
    private readonly ILogger<MistralLectureOcrService> _logger;

    public MistralLectureOcrService(
        HttpClient httpClient,
        IOptions<MistralOcrOptions> options,
        ILogger<MistralLectureOcrService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<string>> ExtractMarkdownAsync(
        Stream image,
        string contentType,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured())
            return Result<string>.Failure(new Error("Ocr.NotConfigured", "Mistral OCR is not configured."));

        var requestBody = await CreateRequestBodyAsync(image, contentType, cancellationToken);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (!Uri.TryCreate(_options.BaseUrl?.TrimEnd('/') + "/v1/ocr", UriKind.Absolute, out var endpoint))
                return Result<string>.Failure(new Error("Ocr.InvalidBaseUrl", "Invalid Mistral OCR base URL."));

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(requestBody, SerializerOptions),
                    Encoding.UTF8,
                    "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return Result<string>.Failure(new Error("Ocr.RateLimited", "OCR provider rate limit exceeded."));

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Mistral OCR request failed with status {StatusCode} after {ElapsedMilliseconds}ms. Error: {ErrorContent}",
                    (int)response.StatusCode,
                    stopwatch.ElapsedMilliseconds,
                    errorContent);
                return Result<string>.Failure(new Error("Ocr.ProviderUnavailable", "OCR provider is currently unavailable."));
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var payload = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
            
            var markdown = ExtractRawMarkdown(payload.RootElement);
            if (string.IsNullOrWhiteSpace(markdown))
                return Result<string>.Failure(new Error("Ocr.EmptyResult", "OCR did not return any readable text."));

            _logger.LogInformation(
                "Mistral OCR lecture extraction completed with model {Model} in {ElapsedMilliseconds}ms.",
                _options.Model,
                stopwatch.ElapsedMilliseconds);

            return Result<string>.Success(markdown);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<string>.Failure(new Error("Ocr.Timeout", "OCR request timed out."));
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "HTTP error during OCR request.");
            return Result<string>.Failure(new Error("Ocr.ProviderUnavailable", "Failed to connect to OCR provider."));
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "JSON parsing error during OCR response.");
            return Result<string>.Failure(new Error("Ocr.InvalidResponse", "Received an invalid response from OCR provider."));
        }
    }

    private async Task<object> CreateRequestBodyAsync(
        Stream image,
        string contentType,
        CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        await image.CopyToAsync(buffer, cancellationToken);

        var dataUrl = $"data:{contentType};base64,{Convert.ToBase64String(buffer.ToArray())}";

        return new
        {
            model = _options.Model,
            document = new
            {
                type = "document_url",
                document_url = dataUrl
            },
            include_blocks = false,
            include_image_base64 = false
        };
    }

    private static string ExtractRawMarkdown(JsonElement root)
    {
        if (!root.TryGetProperty("pages", out var pages) || pages.ValueKind != JsonValueKind.Array)
            return string.Empty;

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            pages.EnumerateArray()
                .Select(page => 
                {
                    if (page.TryGetProperty("markdown", out var property) && property.ValueKind == JsonValueKind.String)
                        return property.GetString();
                    return null;
                })
                .Where(markdown => !string.IsNullOrWhiteSpace(markdown)));
    }

    private bool IsConfigured()
    {
        return IsConfiguredValue(_options.ApiKey) &&
               IsConfiguredValue(_options.BaseUrl) &&
               IsConfiguredValue(_options.Model);
    }

    private static bool IsConfiguredValue(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               !value.StartsWith("your-", StringComparison.OrdinalIgnoreCase);
    }
}
