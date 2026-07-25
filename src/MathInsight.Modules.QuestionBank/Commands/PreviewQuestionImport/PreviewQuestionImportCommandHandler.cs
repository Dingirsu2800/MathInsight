using MathInsight.Modules.QuestionBank.Contracts.Imports;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Imports;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace MathInsight.Modules.QuestionBank.Commands.PreviewQuestionImport;

public sealed class PreviewQuestionImportCommandHandler
    : IRequestHandler<PreviewQuestionImportCommand, Result<QuestionImportPreviewResponse>>
{
    private readonly IQuestionImportWorkbookParser _parser;
    private readonly QuestionImportValidationService _validationService;

    public PreviewQuestionImportCommandHandler(
        IQuestionImportWorkbookParser parser,
        QuestionImportValidationService validationService)
    {
        _parser = parser;
        _validationService = validationService;
    }

    public async Task<Result<QuestionImportPreviewResponse>> Handle(
        PreviewQuestionImportCommand command,
        CancellationToken cancellationToken)
    {
        var fileError = await ValidateFileAsync(command.File, cancellationToken);
        if (fileError is not null)
            return Result<QuestionImportPreviewResponse>.Failure(fileError);

        try
        {
            await using var stream = command.File!.OpenReadStream();
            var workbook = _parser.Parse(stream);
            var preview = await _validationService.BuildPreviewAsync(workbook, command.File.FileName, cancellationToken);
            if (preview.TotalCount > QuestionImportConstants.MaxQuestions)
                return Result<QuestionImportPreviewResponse>.Failure(QuestionBankErrors.QuestionImportLimitExceeded);

            return Result<QuestionImportPreviewResponse>.Success(preview);
        }
        catch (QuestionImportException exception)
        {
            return Result<QuestionImportPreviewResponse>.Failure(exception.Error);
        }
    }

    private static async Task<Error?> ValidateFileAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return QuestionBankErrors.QuestionImportFileRequired;

        if (file.Length > QuestionImportConstants.MaxFileBytes)
            return QuestionBankErrors.QuestionImportFileTooLarge;

        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            return QuestionBankErrors.QuestionImportFileTypeNotSupported;

        await using var stream = file.OpenReadStream();
        var prefix = new byte[4];
        var read = await stream.ReadAsync(prefix, cancellationToken);
        if (read != prefix.Length || prefix[0] != 0x50 || prefix[1] != 0x4B)
            return QuestionBankErrors.QuestionImportFileTypeNotSupported;

        return null;
    }
}
