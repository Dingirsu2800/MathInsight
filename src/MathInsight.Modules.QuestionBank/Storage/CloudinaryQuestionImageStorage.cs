using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MathInsight.Modules.QuestionBank.Configuration;
using Microsoft.Extensions.Options;

namespace MathInsight.Modules.QuestionBank.Storage;

public sealed class CloudinaryQuestionImageStorage : IQuestionImageStorage
{
    private const string Folder = "mathinsight/questions";
    private readonly HttpClient _httpClient;
    private readonly CloudinaryOptions _options;

    public CloudinaryQuestionImageStorage(
        HttpClient httpClient,
        IOptions<CloudinaryOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<string> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured())
            throw new QuestionImageStorageUnavailableException();

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var publicId = Guid.NewGuid().ToString("N");
        var signature = CreateSignature(timestamp, publicId);

        using var multipart = new MultipartFormDataContent();
        using var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

        multipart.Add(fileContent, "file", fileName);
        multipart.Add(new StringContent(_options.ApiKey), "api_key");
        multipart.Add(new StringContent(timestamp.ToString(CultureInfo.InvariantCulture)), "timestamp");
        multipart.Add(new StringContent(Folder), "folder");
        multipart.Add(new StringContent(publicId), "public_id");
        multipart.Add(new StringContent(signature), "signature");

        try
        {
            using var response = await _httpClient.PostAsync(
                $"https://api.cloudinary.com/v1_1/{Uri.EscapeDataString(_options.CloudName)}/image/upload",
                multipart,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new QuestionImageUploadException();

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

    private string CreateSignature(long timestamp, string publicId)
    {
        var valueToSign = $"folder={Folder}&public_id={publicId}&timestamp={timestamp}{_options.ApiSecret}";
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(valueToSign));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool IsConfiguredValue(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            !value.StartsWith("your-", StringComparison.OrdinalIgnoreCase);
    }
}
