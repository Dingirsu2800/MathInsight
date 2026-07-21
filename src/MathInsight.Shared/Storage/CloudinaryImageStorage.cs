using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MathInsight.Shared.Storage;

public sealed class CloudinaryImageStorage : IImageStorage
{
    private readonly HttpClient _httpClient;
    private readonly CloudinaryOptions _options;
    private readonly ILogger<CloudinaryImageStorage> _logger;

    public CloudinaryImageStorage(
        HttpClient httpClient,
        CloudinaryOptions options,
        ILogger<CloudinaryImageStorage> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<string> UploadAsync(
        ImageUploadRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured())
        {
            _logger.LogWarning(
                "Cloudinary image storage is unavailable. CloudName configured: {CloudNameConfigured}; ApiKey configured: {ApiKeyConfigured}; ApiSecret configured: {ApiSecretConfigured}.",
                IsConfiguredValue(_options.CloudName),
                IsConfiguredValue(_options.ApiKey),
                IsConfiguredValue(_options.ApiSecret));
            throw new ImageStorageUnavailableException();
        }

        var publicId = Guid.NewGuid().ToString("N");

        using var multipart = new MultipartFormDataContent();
        using var fileContent = new StreamContent(request.Content);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(request.ContentType);

        multipart.Add(fileContent, "file", request.FileName);
        multipart.Add(CreateFormField(request.Folder), "folder");
        multipart.Add(CreateFormField(publicId), "public_id");

        try
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://api.cloudinary.com/v1_1/{Uri.EscapeDataString(_options.CloudName)}/auto/upload")
            {
                Content = multipart
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ApiKey}:{_options.ApiSecret}")));

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await ReadCloudinaryErrorMessageAsync(response, cancellationToken);
                _logger.LogWarning(
                    "Cloudinary rejected image upload. Status code: {StatusCode}; Error: {ErrorMessage}",
                    (int)response.StatusCode,
                    errorMessage);
                throw new ImageUploadException($"Cloudinary error: {errorMessage}");
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var payload = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);

            if (!payload.RootElement.TryGetProperty("secure_url", out var secureUrlElement) ||
                !Uri.TryCreate(secureUrlElement.GetString(), UriKind.Absolute, out var secureUrl) ||
                secureUrl.Scheme != Uri.UriSchemeHttps)
            {
                throw new ImageUploadException();
            }

            return secureUrl.AbsoluteUri;
        }
        catch (ImageUploadException)
        {
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ImageUploadException();
        }
        catch (HttpRequestException exception)
        {
            throw new ImageUploadException(exception);
        }
        catch (JsonException exception)
        {
            throw new ImageUploadException(exception);
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
            // Cloudinary may return a non-JSON error body.
        }

        return "Cloudinary returned an unexpected error response.";
    }

    private static bool IsConfiguredValue(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            !value.StartsWith("your-", StringComparison.OrdinalIgnoreCase);
    }
}
