using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
            var isUsedInTest = await _context.TestQuestionReadModels
                .AnyAsync(
                    testQuestion => testQuestion.QuestionId == question.QuestionId,
                    cancellationToken);

            if (isUsedInTest)
                return Result<ToggleQuestionActiveResponse>.Failure(QuestionBankErrors.QuestionInUse);
        }

        question.IsActive = command.IsActive;

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

        return Result<ToggleQuestionActiveResponse>.Success(
            new ToggleQuestionActiveResponse(question.QuestionId, question.IsActive, question.Status));
    }
}
