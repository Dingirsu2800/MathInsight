using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Validation;
using MathInsight.Shared.Results;
using MathInsight.Shared.Storage;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Commands.UploadQuestionImage;

public sealed class UploadQuestionImageCommandHandler
    : IRequestHandler<UploadQuestionImageCommand, Result<QuestionImageUploadResponse>>
{
    private const string QuestionImageFolder = "mathinsight/questions";
    private readonly IImageStorage _imageStorage;

    public UploadQuestionImageCommandHandler(IImageStorage imageStorage)
    {
        _imageStorage = imageStorage;
    }

    public async Task<Result<QuestionImageUploadResponse>> Handle(
        UploadQuestionImageCommand command,
        CancellationToken cancellationToken)
    {
        var file = command.File;
        var validationError = await QuestionImageFileValidator.ValidateAsync(file, cancellationToken);
        if (validationError is not null)
            return Result<QuestionImageUploadResponse>.Failure(validationError);

        var declaredContentType = file!.ContentType.Trim();

        try
        {
            await using var uploadStream = file.OpenReadStream();
            var pictureUrl = await _imageStorage.UploadAsync(new ImageUploadRequest(
                uploadStream,
                file.FileName,
                declaredContentType,
                QuestionImageFolder),
                cancellationToken);

            return Result<QuestionImageUploadResponse>.Success(new QuestionImageUploadResponse(pictureUrl));
        }
        catch (ImageStorageUnavailableException)
        {
            return Result<QuestionImageUploadResponse>.Failure(QuestionBankErrors.ImageStorageUnavailable);
        }
        catch (ImageUploadException)
        {
            return Result<QuestionImageUploadResponse>.Failure(QuestionBankErrors.ImageUploadFailed);
        }
    }

}
