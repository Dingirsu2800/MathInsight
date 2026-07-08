using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Commands.UpdateQuestion;

public sealed record UpdateQuestionCommand(
    string QuestionId,
    UpdateQuestionRequest Request,
    string ExpertId) : IRequest<Result<UpdateQuestionResponse>>;
