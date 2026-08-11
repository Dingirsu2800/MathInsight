using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.TestGen.Commands.GenerateFixedBlueprintExam;

public sealed record GenerateFixedBlueprintExamCommand(
    string BlueprintId,
    string ExpertId,
    string TestName,
    int DurationMinutes,
    IReadOnlyList<FixedBlueprintExamQuestionRequest> Questions)
    : IRequest<Result<GenerateSharedBlueprintExamResponse>>;
