using MathInsight.Modules.QuestionBank.Commands.Common;
using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace MathInsight.Modules.QuestionBank.Commands.ToggleQuestionActive;

public sealed class ToggleQuestionActiveCommandHandler
    : IRequestHandler<ToggleQuestionActiveCommand, Result<ToggleQuestionActiveResponse>>
{
    private readonly QuestionBankDbContext _context;

    public ToggleQuestionActiveCommandHandler(QuestionBankDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ToggleQuestionActiveResponse>> Handle(
        ToggleQuestionActiveCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.QuestionId))
            return Result<ToggleQuestionActiveResponse>.Failure(QuestionBankErrors.QuestionIdRequired);

        await using IDbContextTransaction? transaction = QuestionReportSqlServerLock.IsSupported(_context)
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        if (transaction is not null)
            await QuestionReportSqlServerLock.LockQuestionAsync(_context, command.QuestionId, cancellationToken);

        var question = await _context.Questions
            .FirstOrDefaultAsync(
                question => question.QuestionId == command.QuestionId,
                cancellationToken);

        if (question is null)
            return Result<ToggleQuestionActiveResponse>.Failure(QuestionBankErrors.QuestionNotFound);

        if (!string.Equals(question.ExpertId, command.ExpertId, StringComparison.OrdinalIgnoreCase))
            return Result<ToggleQuestionActiveResponse>.Failure(QuestionBankErrors.QuestionMutationForbidden);

        if (!command.IsActive)
        {
            var hasActiveReports = await _context.QuestionReports.AnyAsync(
                report => report.QuestionId == question.QuestionId &&
                          (report.Status == QuestionReportWorkflow.Pending ||
                           report.Status == QuestionReportWorkflow.PendingFix ||
                           report.Status == QuestionReportWorkflow.PendingReview),
                cancellationToken);

            if (hasActiveReports)
                return Result<ToggleQuestionActiveResponse>.Failure(QuestionBankErrors.QuestionHasPendingReports);

            var isUsedInTest = await _context.TestQuestionReadModels
                .AnyAsync(
                    testQuestion => testQuestion.QuestionId == question.QuestionId,
                    cancellationToken);

            if (isUsedInTest)
                return Result<ToggleQuestionActiveResponse>.Failure(QuestionBankErrors.QuestionInUse);
        }

        question.IsActive = command.IsActive;
        question.UpdatedTime = DateTime.UtcNow;

        if (command.IsActive &&
            string.Equals(question.Status, "Deactivated", StringComparison.OrdinalIgnoreCase))
        {
            question.Status = "Approved";
        }
        else if (!command.IsActive)
        {
            question.Status = "Deactivated";
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return Result<ToggleQuestionActiveResponse>.Success(
            new ToggleQuestionActiveResponse(question.QuestionId, question.IsActive, question.Status));
    }
}
