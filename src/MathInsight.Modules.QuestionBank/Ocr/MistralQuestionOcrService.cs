using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MathInsight.Modules.QuestionBank.Configuration;
using MathInsight.Modules.QuestionBank.Contracts.Questions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MathInsight.Modules.QuestionBank.Ocr;

public sealed class MistralQuestionOcrService : IQuestionOcrService
{
    private const decimal LowConfidenceThreshold = 0.80m;
    private const int MaxExtractedImages = 3;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonElement AnnotationSchema = JsonDocument.Parse("""
        {
          "type":"object",
          "additionalProperties":false,
          "required":["questionContent","solutionContent","suggestedQuestionType","answers","parts","warnings"],
          "properties":{
            "questionContent":{"type":"string"},
            "solutionContent":{"type":"string"},
            "suggestedQuestionType":{"type":"string","enum":["SINGLE_CHOICE","MULTIPLE_CHOICE","TRUE_FALSE","SHORT_ANSWER","COMPOSITE","UNKNOWN"]},
            "answers":{"type":"array","maxItems":20,"items":{"type":"object","additionalProperties":false,"required":["content","suggestedIsCorrect"],"properties":{"content":{"type":"string"},"suggestedIsCorrect":{"type":["boolean","null"]}}}},
            "parts":{"type":"array","maxItems":20,"items":{"type":"object","additionalProperties":false,"required":["label","content","partType","explanation","suggestedCorrectBoolean","suggestedCorrectText","suggestedCorrectNumeric","numericTolerance"],"properties":{"label":{"type":["string","null"]},"content":{"type":"string"},"partType":{"type":"string","enum":["TRUE_FALSE","SHORT_ANSWER","NUMERIC_ANSWER","UNKNOWN"]},"explanation":{"type":["string","null"]},"suggestedCorrectBoolean":{"type":["boolean","null"]},"suggestedCorrectText":{"type":["string","null"]},"suggestedCorrectNumeric":{"type":["number","null"]},"numericTolerance":{"type":["number","null"]}}}},
            "warnings":{"type":"array","maxItems":20,"items":{"type":"string"}}
          }
        }
        """).RootElement.Clone();

    private readonly HttpClient _httpClient;
    private readonly MistralOcrOptions _options;
    private readonly ILogger<MistralQuestionOcrService> _logger;

    public MistralQuestionOcrService(
        HttpClient httpClient,
        IOptions<MistralOcrOptions> options,
        ILogger<MistralQuestionOcrService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<QuestionOcrDraftResponse> ExtractDraftAsync(
        Stream image,
        string contentType,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured())
            throw new QuestionOcrNotConfiguredException();

        var requestBody = await CreateRequestBodyAsync(image, contentType, cancellationToken);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, BuildOcrEndpoint())
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(requestBody, SerializerOptions),
                    Encoding.UTF8,
                    "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new QuestionOcrProviderRateLimitedException();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Mistral OCR request failed with status {StatusCode} after {ElapsedMilliseconds}ms.",
                    (int)response.StatusCode,
                    stopwatch.ElapsedMilliseconds);
                throw new QuestionOcrProviderUnavailableException();
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var payload = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
            var result = ParseResponse(payload.RootElement);

            _logger.LogInformation(
                "Mistral OCR draft created with model {Model} in {ElapsedMilliseconds}ms.",
                _options.Model,
                stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (QuestionOcrProviderRateLimitedException)
        {
            throw;
        }
        catch (QuestionOcrDraftUnavailableException)
        {
            throw;
        }
        catch (QuestionOcrInvalidResponseException)
        {
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new QuestionOcrTimeoutException();
        }
        catch (HttpRequestException exception)
        {
            throw new QuestionOcrProviderUnavailableException(exception);
        }
        catch (JsonException exception)
        {
            throw new QuestionOcrInvalidResponseException(exception);
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
                type = "image_url",
                image_url = dataUrl
            },
            confidence_scores_granularity = "page",
            include_blocks = true,
            include_image_base64 = true,
            image_limit = MaxExtractedImages,
            table_format = "markdown",
            document_annotation_prompt = """
                Extract exactly one Vietnamese high-school mathematics question from this image.
                Preserve visible Vietnamese text and mathematical notation using LaTeX with $...$ for inline math and $$...$$ for display math.
                Do not solve the problem, invent missing content, infer an answer key, topic, grade, difficulty, or score.
                Classify only from visible structure. Use COMPOSITE for THPT true/false statement questions and preserve a/b/c/d labels.
                For SINGLE_CHOICE and MULTIPLE_CHOICE, questionContent must contain only the stem. Exclude every answer option and labels such as A/B/C/D from questionContent. Put each option exactly once in answers[].content, without its label.
                For COMPOSITE, questionContent must contain only the shared introduction. Put each a/b/c/d statement exactly once in parts[].content, without its label, and do not duplicate statements in questionContent.
                Set every suggested correct value to null unless the image explicitly marks an official answer or answer key.
                Put uncertain, omitted, or ambiguous information in warnings.
                """,
            document_annotation_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "mathinsight_question_draft",
                    strict = true,
                    schema = AnnotationSchema
                }
            }
        };
    }

    private QuestionOcrDraftResponse ParseResponse(JsonElement root)
    {
        var rawMarkdown = ExtractRawMarkdown(root);
        var pageConfidence = ExtractPageConfidence(root);
        var extractedImages = ExtractImages(root);
        var annotationJson = ExtractAnnotationJson(root);

        OcrAnnotation? annotation;
        try
        {
            annotation = JsonSerializer.Deserialize<OcrAnnotation>(annotationJson, SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new QuestionOcrInvalidResponseException(exception);
        }

        if (annotation is null || string.IsNullOrWhiteSpace(annotation.QuestionContent))
            throw new QuestionOcrDraftUnavailableException();

        var warnings = annotation.Warnings?
            .Select(NormalizeOptionalText)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToList() ?? [];

        if (pageConfidence is not null && pageConfidence < LowConfidenceThreshold)
            warnings.Add("OCR confidence is low; verify all mathematical text and answer suggestions.");

        var questionType = NormalizeQuestionType(annotation.SuggestedQuestionType);
        if (questionType == "UNKNOWN")
            warnings.Add("OCR could not determine the question type; choose it manually.");

        var answers = annotation.Answers?
            .Take(20)
            .Select(answer => new QuestionOcrAnswerDraft(
                NormalizeText(answer.Content),
                answer.SuggestedIsCorrect))
            .Where(answer => !string.IsNullOrWhiteSpace(answer.Content))
            .ToList() ?? [];

        var parts = annotation.Parts?
            .Take(20)
            .Select((part, index) => new QuestionOcrPartDraft(
                NormalizeOptionalText(part.Label) ?? GetDefaultPartLabel(index),
                NormalizeText(part.Content),
                NormalizePartType(part.PartType),
                NormalizeOptionalText(part.Explanation),
                part.SuggestedCorrectBoolean,
                NormalizeOptionalText(part.SuggestedCorrectText),
                part.SuggestedCorrectNumeric,
                part.NumericTolerance))
            .Where(part => !string.IsNullOrWhiteSpace(part.Content))
            .ToList() ?? [];

        if (questionType == "COMPOSITE" && parts.Count == 0)
            warnings.Add("OCR identified a composite question but no valid statements were extracted.");

        if (questionType is "SINGLE_CHOICE" or "MULTIPLE_CHOICE" or "TRUE_FALSE" && answers.Count == 0)
            warnings.Add("OCR identified an option-based question but did not extract answer options.");

        if (extractedImages.Count > 0)
            warnings.Add("OCR detected one or more visual candidates. Select an image manually before attaching it to the question.");

        if (answers.Any(answer => answer.SuggestedIsCorrect is not null) ||
            parts.Any(part => part.SuggestedCorrectBoolean is not null ||
                              part.SuggestedCorrectText is not null ||
                              part.SuggestedCorrectNumeric is not null))
        {
            warnings.Add("OCR answer suggestions are not confirmed answer keys; verify them before saving.");
        }

        return new QuestionOcrDraftResponse(
            rawMarkdown,
            pageConfidence,
            warnings.Distinct(StringComparer.Ordinal).ToList(),
            extractedImages,
            new QuestionOcrDraft(
                NormalizeText(annotation.QuestionContent),
                NormalizeText(annotation.SolutionContent),
                questionType,
                answers,
                parts));
    }

    private static string ExtractRawMarkdown(JsonElement root)
    {
        if (!root.TryGetProperty("pages", out var pages) || pages.ValueKind != JsonValueKind.Array)
            return string.Empty;

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            pages.EnumerateArray()
                .Select(page => TryGetString(page, "markdown"))
                .Where(markdown => !string.IsNullOrWhiteSpace(markdown)));
    }

    private static decimal? ExtractPageConfidence(JsonElement root)
    {
        if (!root.TryGetProperty("pages", out var pages) || pages.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var page in pages.EnumerateArray())
        {
            if (!page.TryGetProperty("confidence_scores", out var scores) ||
                scores.ValueKind != JsonValueKind.Object ||
                !scores.TryGetProperty("average_page_confidence_score", out var confidence) ||
                confidence.ValueKind != JsonValueKind.Number)
            {
                continue;
            }

            if (confidence.TryGetDecimal(out var value))
                return value;
        }

        return null;
    }

    private static IReadOnlyList<QuestionOcrExtractedImage> ExtractImages(JsonElement root)
    {
        if (!root.TryGetProperty("pages", out var pages) || pages.ValueKind != JsonValueKind.Array)
            return [];

        var extractedImages = new List<QuestionOcrExtractedImage>();
        var pageIndex = 0;

        foreach (var page in pages.EnumerateArray())
        {
            if (!page.TryGetProperty("images", out var images) || images.ValueKind != JsonValueKind.Array)
            {
                pageIndex++;
                continue;
            }

            foreach (var image in images.EnumerateArray())
            {
                if (extractedImages.Count >= MaxExtractedImages)
                    return extractedImages;

                var sourceId = TryGetString(image, "id");
                var dataUrl = TryGetString(image, "image_base64");
                if (string.IsNullOrWhiteSpace(sourceId) ||
                    string.IsNullOrWhiteSpace(dataUrl) ||
                    !dataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                extractedImages.Add(new QuestionOcrExtractedImage(
                    $"page-{pageIndex}-{sourceId}",
                    dataUrl,
                    NormalizeOptionalText(TryGetString(image, "image_annotation"))));
            }

            pageIndex++;
        }

        return extractedImages;
    }

    private static string ExtractAnnotationJson(JsonElement root)
    {
        if (!root.TryGetProperty("document_annotation", out var annotation) || annotation.ValueKind == JsonValueKind.Null)
            throw new QuestionOcrInvalidResponseException();

        return annotation.ValueKind switch
        {
            JsonValueKind.String when !string.IsNullOrWhiteSpace(annotation.GetString()) => annotation.GetString()!,
            JsonValueKind.Object => annotation.GetRawText(),
            _ => throw new QuestionOcrInvalidResponseException()
        };
    }

    private Uri BuildOcrEndpoint()
    {
        if (!Uri.TryCreate(_options.BaseUrl?.TrimEnd('/') + "/v1/ocr", UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new QuestionOcrNotConfiguredException();
        }

        return endpoint;
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

    private static string NormalizeQuestionType(string? value)
    {
        return NormalizeToken(value) switch
        {
            "SINGLECHOICE" or "SINGLE" => "SINGLE_CHOICE",
            "MULTIPLECHOICE" or "MULTIPLESELECT" or "MULTIPLE" => "MULTIPLE_CHOICE",
            "TRUEFALSE" => "TRUE_FALSE",
            "SHORTANSWER" => "SHORT_ANSWER",
            "COMPOSITE" => "COMPOSITE",
            _ => "UNKNOWN"
        };
    }

    private static string NormalizePartType(string? value)
    {
        return NormalizeToken(value) switch
        {
            "TRUEFALSE" => "TRUE_FALSE",
            "SHORTANSWER" => "SHORT_ANSWER",
            "NUMERICANSWER" => "NUMERIC_ANSWER",
            _ => "UNKNOWN"
        };
    }

    private static string NormalizeToken(string? value)
    {
        return string.Concat((value ?? string.Empty)
            .Where(char.IsLetterOrDigit))
            .ToUpperInvariant();
    }

    private static string NormalizeText(string? value) => value?.Trim() ?? string.Empty;

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = NormalizeText(value);
        return normalized.Length == 0 ? null : normalized;
    }

    private static string GetDefaultPartLabel(int index) => ((char)('a' + index)).ToString();

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private sealed class OcrAnnotation
    {
        public string? QuestionContent { get; init; }
        public string? SolutionContent { get; init; }
        public string? SuggestedQuestionType { get; init; }
        public List<OcrAnswer>? Answers { get; init; }
        public List<OcrPart>? Parts { get; init; }
        public List<string>? Warnings { get; init; }
    }

    private sealed class OcrAnswer
    {
        public string? Content { get; init; }
        public bool? SuggestedIsCorrect { get; init; }
    }

    private sealed class OcrPart
    {
        public string? Label { get; init; }
        public string? Content { get; init; }
        public string? PartType { get; init; }
        public string? Explanation { get; init; }
        public bool? SuggestedCorrectBoolean { get; init; }
        public string? SuggestedCorrectText { get; init; }
        public decimal? SuggestedCorrectNumeric { get; init; }
        public decimal? NumericTolerance { get; init; }
    }
}
