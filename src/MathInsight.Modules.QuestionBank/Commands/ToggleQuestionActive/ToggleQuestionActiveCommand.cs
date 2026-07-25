using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Commands.ToggleQuestionActive;

public sealed record ToggleQuestionActiveCommand(
    string QuestionId,
    bool IsActive,
    string ExpertId) : IRequest<Result<ToggleQuestionActiveResponse>>;
