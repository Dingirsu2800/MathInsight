using MathInsight.Modules.QuestionBank.Contracts.Tags;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Commands.CreateTagDifficulty;

public sealed record CreateTagDifficultyCommand(
    CreateTagDifficultyRequest Request) : IRequest<Result<TagDifficultyResponse>>;
