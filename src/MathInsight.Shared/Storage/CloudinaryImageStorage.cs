using System.Globalization;
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

        var extension = GetNormalizedExtension(request.FileName);
        var (publicId, safeFileName) = BuildUploadNames(request, extension);

        using var multipart = new MultipartFormDataContent();
        using var fileContent = new StreamContent(request.Content);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(request.ContentType);

        // safeFileName, never request.FileName: MultipartFormDataContent encodes a non-ASCII
        // filename as an RFC 2047 word (=?utf-8?B?...?=), which Cloudinary stores verbatim and
        // sanitises into "utf-8_B_...". That also hides the real extension inside base64, so
        // /auto/upload cannot detect the format and delivers the file without one.
        multipart.Add(fileContent, "file", safeFileName);
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

            // A raw resource is delivered under its public id verbatim, so a missing extension here
            // means the stored object itself has none and every download of it will be unopenable.
            // Surface that instead of silently persisting a broken URL.
            if (request.PreserveFileExtension &&
                extension.Length > 0 &&
                !secureUrl.AbsolutePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Cloudinary returned '{Url}' for '{FileName}', which does not end in the expected '{Extension}'. " +
                    "Downloads of this file will have no extension.",
                    secureUrl.AbsoluteUri,
                    request.FileName,
                    extension);
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

    /// <summary>Lowercased extension including the dot, or empty when the name has none.</summary>
    private static string GetNormalizedExtension(string? fileName)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty);
        return string.IsNullOrWhiteSpace(extension) ? string.Empty : extension.ToLowerInvariant();
    }

    /// <summary>
    /// The public id Cloudinary stores under, and the ASCII filename sent in the multipart part.
    ///
    /// The upload endpoint is <c>/auto/upload</c>, so Cloudinary picks the resource type itself:
    /// images and PDFs become <c>image</c> (which appends the format to the delivery URL on its
    /// own), while Word documents become <c>raw</c> and are served under the public id exactly as
    /// given — hence the extension is baked into the id when the caller opts in.
    ///
    /// Both values are pure ASCII. Cloudinary sanitises characters it does not accept in a public
    /// id, so a Vietnamese name left as-is comes back mangled in the delivery URL; folding it to a
    /// slug up front keeps the URL readable and predictable ("Chứng chỉ.docx" → "chung-chi-1a2b3c4d.docx").
    /// </summary>
    private static (string PublicId, string SafeFileName) BuildUploadNames(
        ImageUploadRequest request,
        string extension)
    {
        var unique = Guid.NewGuid().ToString("N");
        var slug = ToAsciiSlug(Path.GetFileNameWithoutExtension(request.FileName ?? string.Empty));

        if (!request.PreserveFileExtension)
        {
            // Unchanged historical shape for image-only callers: a bare id, Cloudinary appends the
            // detected format to the URL. Only the multipart filename becomes ASCII-safe.
            return (unique, (slug.Length == 0 ? unique : slug) + extension);
        }

        var name = slug.Length == 0 ? unique : $"{slug}-{unique[..8]}";

        return (name + extension, name + extension);
    }

    /// <summary>
    /// Folds a filename to lowercase ASCII: diacritics stripped ("Chứng chỉ" → "chung-chi") and
    /// anything left that is not a letter or digit collapsed into single dashes.
    ///
    /// đ, ư and ơ are mapped by hand: unlike ă/â/ê/ô they are distinct Unicode letters with no
    /// canonical decomposition, so FormD leaves them intact and they would otherwise be dropped as
    /// non-ASCII — turning "Chứng chỉ" into "ch-ng-chi".
    /// </summary>
    private static string ToAsciiSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lower = char.ToLowerInvariant(character);

            if (lower is 'đ' or 'ư' or 'ơ')
            {
                builder.Append(lower switch { 'đ' => 'd', 'ư' => 'u', _ => 'o' });
            }
            else if (lower is >= 'a' and <= 'z' || lower is >= '0' and <= '9')
            {
                builder.Append(lower);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        // Cap the readable part so the public id stays comfortably inside Cloudinary's limit.
        return builder.ToString().Trim('-') is { Length: > 0 } slug
            ? slug[..Math.Min(slug.Length, 60)]
            : string.Empty;
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
