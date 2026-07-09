using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Commands.DeleteQuestion;

public sealed record DeleteQuestionCommand(
    string QuestionId,
    string ExpertId) : IRequest<Result<DeleteQuestionResponse>>;
