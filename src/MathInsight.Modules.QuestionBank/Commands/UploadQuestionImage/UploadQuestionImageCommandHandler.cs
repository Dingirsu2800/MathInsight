using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Storage;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Commands.UploadQuestionImage;

public sealed class UploadQuestionImageCommandHandler
    : IRequestHandler<UploadQuestionImageCommand, Result<QuestionImageUploadResponse>>
{
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;
    private static readonly IReadOnlySet<string> SupportedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private readonly IQuestionImageStorage _imageStorage;

    public UploadQuestionImageCommandHandler(IQuestionImageStorage imageStorage)
    {
        _imageStorage = imageStorage;
    }

    public async Task<Result<QuestionImageUploadResponse>> Handle(
        UploadQuestionImageCommand command,
        CancellationToken cancellationToken)
    {
        var file = command.File;
        if (file is null || file.Length <= 0)
            return Result<QuestionImageUploadResponse>.Failure(QuestionBankErrors.ImageRequired);

        if (file.Length > MaxImageSizeBytes)
            return Result<QuestionImageUploadResponse>.Failure(QuestionBankErrors.ImageTooLarge);

        var declaredContentType = file.ContentType?.Trim();
        if (string.IsNullOrWhiteSpace(declaredContentType) ||
            !SupportedContentTypes.Contains(declaredContentType))
        {
            return Result<QuestionImageUploadResponse>.Failure(QuestionBankErrors.ImageTypeNotSupported);
        }

        await using (var validationStream = file.OpenReadStream())
        {
            var detectedContentType = await DetectContentTypeAsync(validationStream, cancellationToken);
            if (!string.Equals(declaredContentType, detectedContentType, StringComparison.OrdinalIgnoreCase))
                return Result<QuestionImageUploadResponse>.Failure(QuestionBankErrors.ImageTypeNotSupported);
        }

        try
        {
            await using var uploadStream = file.OpenReadStream();
            var pictureUrl = await _imageStorage.UploadAsync(
                uploadStream,
                file.FileName,
                declaredContentType,
                cancellationToken);

            return Result<QuestionImageUploadResponse>.Success(new QuestionImageUploadResponse(pictureUrl));
        }
        catch (QuestionImageStorageUnavailableException)
        {
            return Result<QuestionImageUploadResponse>.Failure(QuestionBankErrors.ImageStorageUnavailable);
        }
        catch (QuestionImageUploadException)
        {
            return Result<QuestionImageUploadResponse>.Failure(QuestionBankErrors.ImageUploadFailed);
        }
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
