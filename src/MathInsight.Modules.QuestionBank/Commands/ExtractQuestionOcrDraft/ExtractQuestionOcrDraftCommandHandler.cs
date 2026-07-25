using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Ocr;
using MathInsight.Modules.QuestionBank.Validation;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Commands.ExtractQuestionOcrDraft;

public sealed class ExtractQuestionOcrDraftCommandHandler
    : IRequestHandler<ExtractQuestionOcrDraftCommand, Result<QuestionOcrDraftResponse>>
{
    private readonly IQuestionOcrService _questionOcrService;

    public ExtractQuestionOcrDraftCommandHandler(IQuestionOcrService questionOcrService)
    {
        _questionOcrService = questionOcrService;
    }

    public async Task<Result<QuestionOcrDraftResponse>> Handle(
        ExtractQuestionOcrDraftCommand command,
        CancellationToken cancellationToken)
    {
        var validationError = await QuestionImageFileValidator.ValidateAsync(command.File, cancellationToken);
        if (validationError is not null)
            return Result<QuestionOcrDraftResponse>.Failure(validationError);

        var file = command.File!;

        try
        {
            await using var imageStream = file.OpenReadStream();
            var draft = await _questionOcrService.ExtractDraftAsync(
                imageStream,
                file.ContentType.Trim(),
                cancellationToken);

            return Result<QuestionOcrDraftResponse>.Success(draft);
        }
        catch (QuestionOcrNotConfiguredException)
        {
            return Result<QuestionOcrDraftResponse>.Failure(QuestionBankErrors.OcrNotConfigured);
        }
        catch (QuestionOcrProviderRateLimitedException)
        {
            return Result<QuestionOcrDraftResponse>.Failure(QuestionBankErrors.OcrProviderRateLimited);
        }
        catch (QuestionOcrTimeoutException)
        {
            return Result<QuestionOcrDraftResponse>.Failure(QuestionBankErrors.OcrTimeout);
        }
        catch (QuestionOcrInvalidResponseException)
        {
            return Result<QuestionOcrDraftResponse>.Failure(QuestionBankErrors.OcrInvalidResponse);
        }
        catch (QuestionOcrDraftUnavailableException)
        {
            return Result<QuestionOcrDraftResponse>.Failure(QuestionBankErrors.OcrDraftUnavailable);
        }
        catch (QuestionOcrProviderUnavailableException)
        {
            return Result<QuestionOcrDraftResponse>.Failure(QuestionBankErrors.OcrProviderUnavailable);
        }
    }
}
