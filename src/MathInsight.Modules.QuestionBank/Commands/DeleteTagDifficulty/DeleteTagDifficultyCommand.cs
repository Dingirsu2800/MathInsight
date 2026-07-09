using MathInsight.Modules.QuestionBank.Contracts.Tags;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Commands.DeleteTagDifficulty;

public sealed record DeleteTagDifficultyCommand(string DifficultyId) : IRequest<Result<DeleteTagResponse>>;
