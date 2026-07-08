using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Commands.CreateQuestion;

public sealed record CreateQuestionCommand(CreateQuestionRequest Request, string ExpertId) : IRequest<Result<CreateQuestionResponse>>;