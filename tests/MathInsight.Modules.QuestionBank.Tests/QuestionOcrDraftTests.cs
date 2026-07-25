using System.Net;
using System.Text;
using System.Text.Json;
using MathInsight.Modules.QuestionBank.Commands.ExtractQuestionOcrDraft;
using MathInsight.Modules.QuestionBank.Configuration;
using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Ocr;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MathInsight.Modules.QuestionBank.Tests;

public sealed class QuestionOcrDraftTests
{
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];

    [Fact]
    public async Task ExtractDraft_WhenFileIsMissing_ReturnsImageRequired()
    {
        var result = await CreateHandler().Handle(
            new ExtractQuestionOcrDraftCommand(null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ImageRequired, result.Error);
    }

    [Fact]
    public async Task ExtractDraft_WhenProviderIsUnavailable_ReturnsStableError()
    {
        var handler = CreateHandler(new StubQuestionOcrService
        {
            ExceptionToThrow = new QuestionOcrProviderUnavailableException()
        });

        var result = await handler.Handle(
            new ExtractQuestionOcrDraftCommand(CreatePngFile()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.OcrProviderUnavailable, result.Error);
    }

    [Theory]
    [InlineData("not-configured")]
    [InlineData("rate-limited")]
    [InlineData("timeout")]
    [InlineData("invalid-response")]
    [InlineData("unavailable-draft")]
    public async Task ExtractDraft_MapsKnownOcrFailures(string failure)
    {
        Exception exception = failure switch
        {
            "not-configured" => new QuestionOcrNotConfiguredException(),
            "rate-limited" => new QuestionOcrProviderRateLimitedException(),
            "timeout" => new QuestionOcrTimeoutException(),
            "invalid-response" => new QuestionOcrInvalidResponseException(),
            "unavailable-draft" => new QuestionOcrDraftUnavailableException(),
            _ => throw new InvalidOperationException("Unexpected test case.")
        };
        var expectedError = failure switch
        {
            "not-configured" => QuestionBankErrors.OcrNotConfigured,
            "rate-limited" => QuestionBankErrors.OcrProviderRateLimited,
            "timeout" => QuestionBankErrors.OcrTimeout,
            "invalid-response" => QuestionBankErrors.OcrInvalidResponse,
            _ => QuestionBankErrors.OcrDraftUnavailable
        };
        var handler = CreateHandler(new StubQuestionOcrService { ExceptionToThrow = exception });

        var result = await handler.Handle(
            new ExtractQuestionOcrDraftCommand(CreatePngFile()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedError, result.Error);
    }

    [Fact]
    public async Task MistralService_WhenConfigurationIsMissing_ThrowsNotConfigured()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(
            (Func<HttpRequestMessage, HttpResponseMessage>)(_ => throw new InvalidOperationException())));
        var service = CreateMistralService(client, new MistralOcrOptions());

        await Assert.ThrowsAsync<QuestionOcrNotConfiguredException>(() =>
            service.ExtractDraftAsync(new MemoryStream(PngHeader), "image/png", CancellationToken.None));
    }

    [Fact]
    public async Task MistralService_WhenResponseIsValid_ReturnsUnpersistedDraft()
    {
        string? requestJson = null;
        using var client = new HttpClient(new StubHttpMessageHandler(async request =>
        {
            requestJson = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CreateProviderResponse(), Encoding.UTF8, "application/json")
            };
        }));
        var service = CreateMistralService(client);

        var result = await service.ExtractDraftAsync(
            new MemoryStream(PngHeader),
            "image/png",
            CancellationToken.None);

        Assert.Equal("Tính $x$.", result.Draft.QuestionContent);
        Assert.Equal("SINGLE_CHOICE", result.Draft.SuggestedQuestionType);
        Assert.Single(result.Draft.Answers);
        Assert.False(result.Draft.Answers[0].SuggestedIsCorrect ?? true);
        Assert.Equal(0.91m, result.PageConfidence);
        Assert.Single(result.ExtractedImages);
        Assert.Equal("data:image/png;base64,diagram", result.ExtractedImages[0].DataUrl);
        Assert.NotNull(requestJson);
        Assert.Contains("document_annotation_format", requestJson);
        Assert.Contains("data:image/png;base64", requestJson);
        Assert.Contains("\"include_image_base64\":true", requestJson);
    }

    [Fact]
    public async Task MistralService_WhenProviderRateLimits_ThrowsRateLimited()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.TooManyRequests)));
        var service = CreateMistralService(client);

        await Assert.ThrowsAsync<QuestionOcrProviderRateLimitedException>(() =>
            service.ExtractDraftAsync(new MemoryStream(PngHeader), "image/png", CancellationToken.None));
    }

    [Fact]
    public async Task MistralService_WhenAnnotationHasNoQuestion_ThrowsDraftUnavailable()
    {
        var annotation = new
        {
            questionContent = " ", solutionContent = "", suggestedQuestionType = "UNKNOWN",
            answers = Array.Empty<object>(), parts = Array.Empty<object>(), warnings = Array.Empty<string>()
        };
        var payload = JsonSerializer.Serialize(new { pages = Array.Empty<object>(), document_annotation = JsonSerializer.Serialize(annotation) });
        using var client = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        }));
        var service = CreateMistralService(client);

        await Assert.ThrowsAsync<QuestionOcrDraftUnavailableException>(() =>
            service.ExtractDraftAsync(new MemoryStream(PngHeader), "image/png", CancellationToken.None));
    }

    private static ExtractQuestionOcrDraftCommandHandler CreateHandler(IQuestionOcrService? service = null) =>
        new(service ?? new StubQuestionOcrService());

    private static MistralQuestionOcrService CreateMistralService(
        HttpClient client,
        MistralOcrOptions? options = null) =>
        new(
            client,
            Options.Create(options ?? new MistralOcrOptions { ApiKey = "test-key" }),
            NullLogger<MistralQuestionOcrService>.Instance);

    private static IFormFile CreatePngFile()
    {
        var stream = new MemoryStream(PngHeader);
        return new FormFile(stream, 0, PngHeader.Length, "file", "question.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };
    }

    private static string CreateProviderResponse()
    {
        var annotation = new
        {
            questionContent = "Tính $x$.", solutionContent = "", suggestedQuestionType = "SINGLE_CHOICE",
            answers = new[] { new { content = "$x=1$", suggestedIsCorrect = false } },
            parts = Array.Empty<object>(), warnings = Array.Empty<string>()
        };
        return JsonSerializer.Serialize(new
        {
            pages = new[]
            {
                new
                {
                    markdown = "Tính $x$.",
                    confidence_scores = new { average_page_confidence_score = 0.91m },
                    blocks = Array.Empty<object>(),
                    images = new[] { new { id = "diagram-1", image_base64 = "data:image/png;base64,diagram", image_annotation = "A geometry diagram." } }
                }
            },
            document_annotation = JsonSerializer.Serialize(annotation)
        });
    }

    private sealed class StubQuestionOcrService : IQuestionOcrService
    {
        public Exception? ExceptionToThrow { get; init; }

        public Task<QuestionOcrDraftResponse> ExtractDraftAsync(Stream image, string contentType, CancellationToken cancellationToken)
        {
            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;

            return Task.FromResult(new QuestionOcrDraftResponse(
                string.Empty, null, [], [], new QuestionOcrDraft(string.Empty, string.Empty, "UNKNOWN", [], [])));
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            : this(request => Task.FromResult(handler(request)))
        {
        }

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _handler(request);
    }
}
