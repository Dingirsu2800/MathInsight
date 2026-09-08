using System.Data;
using MathInsight.Modules.QuestionBank.Commands.Common;
using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Modules.QuestionBank.Services;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MathInsight.Modules.QuestionBank.Commands.UpdateQuestion;

public sealed class UpdateQuestionCommandHandler : IRequestHandler<UpdateQuestionCommand, Result<UpdateQuestionResponse>>
{
    private readonly QuestionBankDbContext _context;
    private readonly QuestionMutationService _mutationService;

    public UpdateQuestionCommandHandler(QuestionBankDbContext context)
    {
        _context = context;
        _mutationService = new QuestionMutationService(context);
    }

    public async Task<Result<UpdateQuestionResponse>> Handle(UpdateQuestionCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.QuestionId))
            return Result<UpdateQuestionResponse>.Failure(QuestionBankErrors.QuestionIdRequired);

        return await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using IDbContextTransaction? transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;

            if (transaction is not null && QuestionReportSqlServerLock.IsSupported(_context))
                await QuestionReportSqlServerLock.LockQuestionAsync(_context, command.QuestionId, cancellationToken);

            var mutation = await _mutationService.ApplyAsync(command.QuestionId, command.Request, command.ExpertId, cancellationToken);
            if (mutation.IsFailure)
                return Result<UpdateQuestionResponse>.Failure(mutation.Error!);

            await _context.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);

            return Result<UpdateQuestionResponse>.Success(new UpdateQuestionResponse(
                mutation.Value!.Question.QuestionId,
                mutation.Value.Question.Status,
                true));
        });
    }
}
