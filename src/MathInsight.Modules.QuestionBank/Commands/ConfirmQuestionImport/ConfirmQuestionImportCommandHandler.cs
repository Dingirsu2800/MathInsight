using MathInsight.Modules.QuestionBank.Contracts.Imports;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Imports;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Modules.QuestionBank.Validation;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MathInsight.Modules.QuestionBank.Commands.ConfirmQuestionImport;

public sealed class ConfirmQuestionImportCommandHandler
    : IRequestHandler<ConfirmQuestionImportCommand, Result<QuestionImportConfirmResponse>>
{
    private readonly QuestionBankDbContext _context;
    private readonly QuestionImportValidationService _validationService;

    public ConfirmQuestionImportCommandHandler(
        QuestionBankDbContext context,
        QuestionImportValidationService validationService)
    {
        _context = context;
        _validationService = validationService;
    }

    public async Task<Result<QuestionImportConfirmResponse>> Handle(
        ConfirmQuestionImportCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        if (string.IsNullOrWhiteSpace(request.ImportId))
        {
            return Result<QuestionImportConfirmResponse>.Success(Invalid(request.ImportId, [
                Issue("Confirm", null, "ImportId", null, QuestionBankErrors.QuestionImportIdInvalid.Message, QuestionBankErrors.QuestionImportIdInvalid)
            ]));
        }

        if (request.Items is null || request.Items.Count == 0)
            return Result<QuestionImportConfirmResponse>.Success(Invalid(request.ImportId, [
                Issue("Confirm", null, "Items", null, "At least one valid preview item must be selected.")
            ]));

        if (request.Items.Count > QuestionImportConstants.MaxQuestions)
            return Result<QuestionImportConfirmResponse>.Failure(QuestionBankErrors.QuestionImportLimitExceeded);

        var issues = new List<QuestionImportIssueResponse>();
        var candidates = new List<QuestionImportCandidate>();
        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.QuestionKey) || item.QuestionKey.Length > 50)
            {
                issues.Add(Issue("Confirm", null, "QuestionKey", item.QuestionKey, QuestionBankErrors.QuestionImportQuestionKeyInvalid.Message, QuestionBankErrors.QuestionImportQuestionKeyInvalid));
                continue;
            }

            if (item.Draft is null)
            {
                issues.Add(Issue("Confirm", null, "Draft", item.QuestionKey, "A normalized question draft is required."));
                continue;
            }

            candidates.Add(new QuestionImportCandidate(item.QuestionKey.Trim(), item.Draft));
        }

        foreach (var duplicate in candidates.GroupBy(candidate => candidate.QuestionKey, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
        {
            issues.Add(Issue("Confirm", null, "QuestionKey", duplicate.Key, QuestionBankErrors.QuestionImportQuestionKeyDuplicate.Message, QuestionBankErrors.QuestionImportQuestionKeyDuplicate));
        }

        if (issues.Count > 0)
            return Result<QuestionImportConfirmResponse>.Success(Invalid(request.ImportId, issues));

        issues.AddRange(await _validationService.ValidateConfirmAsync(candidates, cancellationToken));
        if (issues.Count > 0)
            return Result<QuestionImportConfirmResponse>.Success(Invalid(request.ImportId, issues));

        var questions = new List<(string QuestionKey, string QuestionId)>();
        foreach (var candidate in candidates)
        {
            QuestionRequestValidator.Validate(candidate.Draft, out var databaseQuestionType);
            var question = QuestionImportQuestionFactory.Create(candidate.Draft, command.ExpertId, databaseQuestionType!);
            _context.Questions.Add(question);
            questions.Add((candidate.QuestionKey, question.QuestionId));
        }

        await using IDbContextTransaction? transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        await _context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return Result<QuestionImportConfirmResponse>.Success(new QuestionImportConfirmResponse(
            string.Empty,
            request.ImportId,
            questions.Count,
            questions.Select(question => new ImportedQuestionResponse(question.QuestionKey, question.QuestionId)).ToList(),
            []));
    }

    private static QuestionImportConfirmResponse Invalid(
        string importId,
        IReadOnlyList<QuestionImportIssueResponse> errors) => new(
            QuestionBankErrors.QuestionImportValidationFailed.Code,
            importId,
            0,
            [],
            errors);

    private static QuestionImportIssueResponse Issue(
        string sheet,
        int? row,
        string? column,
        string? questionKey,
        string message,
        Error? error = null) => new(
            error?.Code ?? QuestionBankErrors.QuestionImportValidationFailed.Code,
            message,
            sheet,
            row,
            column,
            questionKey);
}
