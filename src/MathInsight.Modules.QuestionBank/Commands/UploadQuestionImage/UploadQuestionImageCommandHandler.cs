using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Storage;
using MathInsight.Modules.QuestionBank.Validation;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Commands.UploadQuestionImage;

public sealed class UploadQuestionImageCommandHandler
    : IRequestHandler<UploadQuestionImageCommand, Result<QuestionImageUploadResponse>>
{
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
        var validationError = await QuestionImageFileValidator.ValidateAsync(file, cancellationToken);
        if (validationError is not null)
            return Result<QuestionImageUploadResponse>.Failure(validationError);

        var declaredContentType = file!.ContentType.Trim();

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

}
