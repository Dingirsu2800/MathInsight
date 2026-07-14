using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Imports;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Modules.QuestionBank.Validation;
using MathInsight.Shared.Results;
using MediatR;

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
        _context.Questions.Add(question);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<CreateQuestionResponse>.Success(
            new CreateQuestionResponse(question.QuestionId, question.Status));
    }
}
