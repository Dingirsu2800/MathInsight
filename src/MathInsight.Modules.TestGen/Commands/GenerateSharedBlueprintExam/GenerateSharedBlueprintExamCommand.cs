using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.TestGen.Commands.GenerateSharedBlueprintExam;

public sealed record GenerateSharedBlueprintExamCommand(
    string BlueprintId,
    string ExpertId,
    string TestName,
    int DurationMinutes) : IRequest<Result<GenerateSharedBlueprintExamResponse>>;
