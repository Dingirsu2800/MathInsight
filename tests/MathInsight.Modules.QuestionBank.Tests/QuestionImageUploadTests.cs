using System.Net;
using System.Text;
using MathInsight.Modules.QuestionBank.Commands.UploadQuestionImage;
using MathInsight.Modules.QuestionBank.Configuration;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace MathInsight.Modules.QuestionBank.Tests;

public sealed class QuestionImageUploadTests
{
    [Fact]
    public async Task UploadImage_WhenFileIsMissing_ReturnsImageRequired()
    {
        var result = await CreateHandler().Handle(
            new UploadQuestionImageCommand(null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ImageRequired, result.Error);
    }

    [Fact]
    public async Task UploadImage_WhenFileIsEmpty_ReturnsImageRequired()
    {
        var result = await CreateHandler().Handle(
            new UploadQuestionImageCommand(CreateFile([], "image/png", "empty.png")),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ImageRequired, result.Error);
    }

    [Fact]
    public async Task UploadImage_WhenFileExceedsFiveMegabytes_ReturnsImageTooLarge()
    {
        var file = CreateFile(new byte[(5 * 1024 * 1024) + 1], "image/png", "large.png");

        var result = await CreateHandler().Handle(
            new UploadQuestionImageCommand(file),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ImageTooLarge, result.Error);
    }

    [Fact]
    public async Task UploadImage_WhenMimeTypeIsUnsupported_ReturnsImageTypeNotSupported()
    {
        var file = CreateFile(Encoding.UTF8.GetBytes("not-an-image"), "application/pdf", "image.pdf");

        var result = await CreateHandler().Handle(
            new UploadQuestionImageCommand(file),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ImageTypeNotSupported, result.Error);
    }

    [Fact]
    public async Task UploadImage_WhenMagicBytesDoNotMatchMimeType_ReturnsImageTypeNotSupported()
    {
        var file = CreateFile(JpegHeader, "image/png", "forged.png");

        var result = await CreateHandler().Handle(
            new UploadQuestionImageCommand(file),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ImageTypeNotSupported, result.Error);
    }

    [Theory]
    [MemberData(nameof(ValidImages))]
    public async Task UploadImage_WhenImageIsValid_ReturnsStorageUrl(
        byte[] content,
        string contentType,
        string fileName)
    {
        var storage = new StubQuestionImageStorage
        {
            PictureUrl = "https://res.cloudinary.com/mathinsight/image/upload/test.webp"
        };
        var handler = new UploadQuestionImageCommandHandler(storage);
        var file = CreateFile(content, contentType, fileName);

        var result = await handler.Handle(new UploadQuestionImageCommand(file), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(storage.PictureUrl, result.Value!.PictureUrl);
        Assert.Equal(contentType, storage.ContentType);
        Assert.Equal(fileName, storage.FileName);
    }

    [Fact]
    public async Task UploadImage_WhenStorageIsNotConfigured_ReturnsUnavailableError()
    {
        var handler = new UploadQuestionImageCommandHandler(
            new StubQuestionImageStorage { ExceptionToThrow = new QuestionImageStorageUnavailableException() });

        var result = await handler.Handle(
            new UploadQuestionImageCommand(CreateFile(PngHeader, "image/png", "image.png")),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ImageStorageUnavailable, result.Error);
    }

    [Fact]
    public async Task UploadImage_WhenStorageFails_ReturnsUploadFailedError()
    {
        var handler = new UploadQuestionImageCommandHandler(
            new StubQuestionImageStorage { ExceptionToThrow = new QuestionImageUploadException() });

        var result = await handler.Handle(
            new UploadQuestionImageCommand(CreateFile(PngHeader, "image/png", "image.png")),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ImageUploadFailed, result.Error);
    }

    [Theory]
    [InlineData("CloudName")]
    [InlineData("ApiKey")]
    [InlineData("ApiSecret")]
    public async Task CloudinaryStorage_WhenARequiredSettingIsMissing_ThrowsUnavailableException(string missingSetting)
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException()));
        var options = new CloudinaryOptions
        {
            CloudName = "mathinsight",
            ApiKey = "api-key",
            ApiSecret = "api-secret"
        };
        options = missingSetting switch
        {
            "CloudName" => new CloudinaryOptions { ApiKey = options.ApiKey, ApiSecret = options.ApiSecret },
            "ApiKey" => new CloudinaryOptions { CloudName = options.CloudName, ApiSecret = options.ApiSecret },
            "ApiSecret" => new CloudinaryOptions { CloudName = options.CloudName, ApiKey = options.ApiKey },
            _ => throw new InvalidOperationException("Unexpected setting.")
        };
        var storage = new CloudinaryQuestionImageStorage(
            client,
            Options.Create(options));

        await Assert.ThrowsAsync<QuestionImageStorageUnavailableException>(() =>
            storage.UploadAsync(new MemoryStream(PngHeader), "image.png", "image/png", CancellationToken.None));
    }

    [Fact]
    public async Task CloudinaryStorage_WhenCloudinaryReturnsError_ThrowsUploadException()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)));
        var storage = CreateCloudinaryStorage(client);

        await Assert.ThrowsAsync<QuestionImageUploadException>(() =>
            storage.UploadAsync(new MemoryStream(PngHeader), "image.png", "image/png", CancellationToken.None));
    }

    [Fact]
    public async Task CloudinaryStorage_WhenResponseHasNoSecureUrl_ThrowsUploadException()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"public_id\":\"image\"}", Encoding.UTF8, "application/json")
            }));
        var storage = CreateCloudinaryStorage(client);

        await Assert.ThrowsAsync<QuestionImageUploadException>(() =>
            storage.UploadAsync(new MemoryStream(PngHeader), "image.png", "image/png", CancellationToken.None));
    }

    [Fact]
    public async Task CloudinaryStorage_WhenRequestTimesOut_ThrowsUploadException()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ => throw new OperationCanceledException()));
        var storage = CreateCloudinaryStorage(client);

        await Assert.ThrowsAsync<QuestionImageUploadException>(() =>
            storage.UploadAsync(new MemoryStream(PngHeader), "image.png", "image/png", CancellationToken.None));
    }

    [Fact]
    public async Task CloudinaryStorage_WhenResponseIsValid_ReturnsSecureUrl()
    {
        HttpRequestMessage? capturedRequest = null;
        using var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"secure_url\":\"https://res.cloudinary.com/mathinsight/image/upload/test.png\"}",
                    Encoding.UTF8,
                    "application/json")
            };
        }));
        var storage = CreateCloudinaryStorage(client);

        var pictureUrl = await storage.UploadAsync(
            new MemoryStream(PngHeader),
            "image.png",
            "image/png",
            CancellationToken.None);

        Assert.Equal("https://res.cloudinary.com/mathinsight/image/upload/test.png", pictureUrl);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal("https://api.cloudinary.com/v1_1/mathinsight/image/upload", capturedRequest.RequestUri!.AbsoluteUri);
    }

    public static IEnumerable<object[]> ValidImages =>
    [
        [JpegHeader, "image/jpeg", "image.jpg"],
        [PngHeader, "image/png", "image.png"],
        [WebpHeader, "image/webp", "image.webp"]
    ];

    private static readonly byte[] JpegHeader = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
    private static readonly byte[] WebpHeader = [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50];

    private static UploadQuestionImageCommandHandler CreateHandler()
    {
        return new UploadQuestionImageCommandHandler(new StubQuestionImageStorage());
    }

    private static CloudinaryQuestionImageStorage CreateCloudinaryStorage(HttpClient client)
    {
        return new CloudinaryQuestionImageStorage(
            client,
            Options.Create(new CloudinaryOptions
            {
                CloudName = "mathinsight",
                ApiKey = "api-key",
                ApiSecret = "api-secret"
            }));
    }

    private static IFormFile CreateFile(byte[] content, string contentType, string fileName)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private sealed class StubQuestionImageStorage : IQuestionImageStorage
    {
        public string PictureUrl { get; init; } = "https://res.cloudinary.com/mathinsight/image/upload/default.png";
        public Exception? ExceptionToThrow { get; init; }
        public string? FileName { get; private set; }
        public string? ContentType { get; private set; }

        public Task<string> UploadAsync(
            Stream content,
            string fileName,
            string contentType,
            CancellationToken cancellationToken)
        {
            FileName = fileName;
            ContentType = contentType;

            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;

            return Task.FromResult(PictureUrl);
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_responseFactory(request));
        }
    }
}
