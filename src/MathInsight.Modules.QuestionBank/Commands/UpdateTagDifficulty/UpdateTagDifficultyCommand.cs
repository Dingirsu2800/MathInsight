using MathInsight.Modules.QuestionBank.Contracts.Tags;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Commands.UpdateTagDifficulty;

public sealed record UpdateTagDifficultyCommand(
    string DifficultyId,
    UpdateTagDifficultyRequest Request) : IRequest<Result<TagDifficultyResponse>>;
