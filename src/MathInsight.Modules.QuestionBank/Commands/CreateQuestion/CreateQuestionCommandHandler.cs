using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Commands.Common;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Imports;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Modules.QuestionBank.Validation;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MathInsight.Modules.QuestionBank.Commands.CreateQuestion;

public sealed class CreateQuestionCommandHandler
    : IRequestHandler<CreateQuestionCommand, Result<CreateQuestionResponse>>
{
    private readonly QuestionBankDbContext _context;

    public CreateQuestionCommandHandler(QuestionBankDbContext context)
    {
        _context = context;
    }

    public async Task<Result<CreateQuestionResponse>> Handle(
        CreateQuestionCommand command,
        CancellationToken cancellationToken)
    {
        var validationError = QuestionRequestValidator.Validate(command.Request, out var databaseQuestionType);
        if (validationError is not null)
            return Result<CreateQuestionResponse>.Failure(validationError);

        var question = QuestionImportQuestionFactory.Create(
            command.Request,
            command.ExpertId,
            databaseQuestionType!);

        var now = DateTime.UtcNow;
        question.CreatedTime = now;
        question.UpdatedTime = now;

        await using IDbContextTransaction? transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        _context.Questions.Add(question);
        _context.QuestionVersions.Add(
            QuestionVersionSnapshotFactory.Create(question, command.ExpertId, 1, now));
        await _context.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return Result<CreateQuestionResponse>.Success(
            new CreateQuestionResponse(question.QuestionId, question.Status));
    }
}
