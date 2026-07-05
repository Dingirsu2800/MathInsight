using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Commands.CreateQuestion;

public sealed record CreateQuestionCommand(CreateQuestionRequest Request, string ExpertId) : IRequest<CreateQuestionResponse>;