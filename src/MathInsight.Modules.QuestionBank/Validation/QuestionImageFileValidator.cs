using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Shared.Results;
using Microsoft.AspNetCore.Http;

namespace MathInsight.Modules.QuestionBank.Validation;

public static class QuestionImageFileValidator
{
    public const long MaxImageSizeBytes = 5 * 1024 * 1024;

    private static readonly IReadOnlySet<string> SupportedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    public static async Task<Error?> ValidateAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length <= 0)
            return QuestionBankErrors.ImageRequired;

        if (file.Length > MaxImageSizeBytes)
            return QuestionBankErrors.ImageTooLarge;

        var declaredContentType = file.ContentType?.Trim();
        if (string.IsNullOrWhiteSpace(declaredContentType) ||
            !SupportedContentTypes.Contains(declaredContentType))
        {
            return QuestionBankErrors.ImageTypeNotSupported;
        }

        await using var validationStream = file.OpenReadStream();
        var detectedContentType = await DetectContentTypeAsync(validationStream, cancellationToken);

        return string.Equals(declaredContentType, detectedContentType, StringComparison.OrdinalIgnoreCase)
            ? null
            : QuestionBankErrors.ImageTypeNotSupported;
    }

    private static async Task<string?> DetectContentTypeAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[12];
        var bytesRead = 0;

        while (bytesRead < header.Length)
        {
            var read = await stream.ReadAsync(header.AsMemory(bytesRead), cancellationToken);
            if (read == 0)
                break;

            bytesRead += read;
        }

        if (bytesRead >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return "image/jpeg";

        if (bytesRead >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
        {
            return "image/png";
        }

        if (bytesRead >= 12 &&
            header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
        {
            return "image/webp";
        }

        return null;
    }
}
