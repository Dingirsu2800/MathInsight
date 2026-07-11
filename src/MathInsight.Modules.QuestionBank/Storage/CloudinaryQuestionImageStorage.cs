using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MathInsight.Modules.QuestionBank.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MathInsight.Modules.QuestionBank.Storage;

public sealed class CloudinaryQuestionImageStorage : IQuestionImageStorage
{
    private const string Folder = "mathinsight/questions";
    private readonly HttpClient _httpClient;
    private readonly CloudinaryOptions _options;
    private readonly ILogger<CloudinaryQuestionImageStorage> _logger;

    public CloudinaryQuestionImageStorage(
        HttpClient httpClient,
        IOptions<CloudinaryOptions> options,
        ILogger<CloudinaryQuestionImageStorage> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured())
        {
            _logger.LogWarning(
                "Question image storage is unavailable. CloudName configured: {CloudNameConfigured}; ApiKey configured: {ApiKeyConfigured}; ApiSecret configured: {ApiSecretConfigured}.",
                IsConfiguredValue(_options.CloudName),
                IsConfiguredValue(_options.ApiKey),
                IsConfiguredValue(_options.ApiSecret));
            throw new QuestionImageStorageUnavailableException();
        }

        var publicId = Guid.NewGuid().ToString("N");

        await using var fileBuffer = new MemoryStream();
        await content.CopyToAsync(fileBuffer, cancellationToken);
        using var multipart = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(fileBuffer.ToArray());
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

        multipart.Add(fileContent, "file", fileName);
        multipart.Add(CreateFormField(Folder), "folder");
        multipart.Add(CreateFormField(publicId), "public_id");

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://api.cloudinary.com/v1_1/{Uri.EscapeDataString(_options.CloudName)}/image/upload")
            {
                Content = multipart
            };
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ApiKey}:{_options.ApiSecret}")));

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await ReadCloudinaryErrorMessageAsync(response, cancellationToken);
                _logger.LogWarning(
                    "Cloudinary rejected question image upload. Status code: {StatusCode}; Error: {ErrorMessage}",
                    (int)response.StatusCode,
                    errorMessage);
                throw new QuestionImageUploadException();
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var payload = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);

            if (!payload.RootElement.TryGetProperty("secure_url", out var secureUrlElement) ||
                !Uri.TryCreate(secureUrlElement.GetString(), UriKind.Absolute, out var secureUrl) ||
                secureUrl.Scheme != Uri.UriSchemeHttps)
            {
                throw new QuestionImageUploadException();
            }

            return secureUrl.AbsoluteUri;
        }
        catch (QuestionImageUploadException)
        {
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new QuestionImageUploadException();
        }
        catch (HttpRequestException exception)
        {
            throw new QuestionImageUploadException(exception);
        }
        catch (JsonException exception)
        {
            throw new QuestionImageUploadException(exception);
        }
    }

    private bool IsConfigured()
    {
        return IsConfiguredValue(_options.CloudName) &&
            IsConfiguredValue(_options.ApiKey) &&
            IsConfiguredValue(_options.ApiSecret);
    }

    private static ByteArrayContent CreateFormField(string value)
    {
        return new ByteArrayContent(Encoding.UTF8.GetBytes(value));
    }

    private static async Task<string> ReadCloudinaryErrorMessageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var payload = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);

            if (payload.RootElement.TryGetProperty("error", out var errorElement) &&
                errorElement.TryGetProperty("message", out var messageElement) &&
                messageElement.ValueKind == JsonValueKind.String)
            {
                return messageElement.GetString() ?? "Cloudinary returned no error message.";
            }
        }
        catch (JsonException)
        {
            // A generic message is sufficient when Cloudinary does not return the expected JSON error shape.
        }

        return "Cloudinary returned an unexpected error response.";
    }

    private static bool IsConfiguredValue(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            !value.StartsWith("your-", StringComparison.OrdinalIgnoreCase);
    }
}
