using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.TestGen.Commands.SubmitBlueprintForReview;

public sealed record SubmitBlueprintForReviewCommand(
    string BlueprintId,
    string ExpertId) : IRequest<Result<SubmitBlueprintResponse>>;
