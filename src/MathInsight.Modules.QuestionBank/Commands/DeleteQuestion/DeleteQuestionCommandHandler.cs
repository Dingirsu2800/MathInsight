using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Commands.DeleteQuestion;

public sealed class DeleteQuestionCommandHandler
    : IRequestHandler<DeleteQuestionCommand, Result<DeleteQuestionResponse>>
{
    private readonly QuestionBankDbContext _context;

    public DeleteQuestionCommandHandler(QuestionBankDbContext context)
    {
        _context = context;
    }

    public async Task<Result<DeleteQuestionResponse>> Handle(
        DeleteQuestionCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.QuestionId))
            return Result<DeleteQuestionResponse>.Failure(QuestionBankErrors.QuestionIdRequired);

        var question = await _context.Questions
            .Include(question => question.Answers)
            .Include(question => question.Parts)
            .Include(question => question.QuestionTopics)
            .Include(question => question.Versions)
            .Include(question => question.Reports)
            .FirstOrDefaultAsync(
                question => question.QuestionId == command.QuestionId,
                cancellationToken);

        if (question is null)
            return Result<DeleteQuestionResponse>.Failure(QuestionBankErrors.QuestionNotFound);

        if (!string.Equals(question.ExpertId, command.ExpertId, StringComparison.OrdinalIgnoreCase))
            return Result<DeleteQuestionResponse>.Failure(QuestionBankErrors.QuestionMutationForbidden);

        var isUsedInTest = await _context.TestQuestionReadModels
            .AnyAsync(
                testQuestion => testQuestion.QuestionId == question.QuestionId,
                cancellationToken);

        if (isUsedInTest)
        {
            question.IsActive = false;
            question.Status = "Deactivated";

            await _context.SaveChangesAsync(cancellationToken);

            return Result<DeleteQuestionResponse>.Success(
                new DeleteQuestionResponse(question.QuestionId, "SoftDeleted", question.IsActive, question.Status));
        }

        _context.QuestionReports.RemoveRange(question.Reports);
        _context.QuestionVersions.RemoveRange(question.Versions);
        _context.QuestionTopics.RemoveRange(question.QuestionTopics);
        _context.QuestionParts.RemoveRange(question.Parts);
        _context.Answers.RemoveRange(question.Answers);
        _context.Questions.Remove(question);

        await _context.SaveChangesAsync(cancellationToken);

        return Result<DeleteQuestionResponse>.Success(
            new DeleteQuestionResponse(command.QuestionId, "HardDeleted", false, "Deleted"));
    }
}
