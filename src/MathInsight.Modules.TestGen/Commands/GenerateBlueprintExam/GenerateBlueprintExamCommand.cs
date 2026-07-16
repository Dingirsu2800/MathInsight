using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.TestGen.Commands.GenerateBlueprintExam;

public sealed record GenerateBlueprintExamCommand(
    string BlueprintId,
    string StudentId) : IRequest<Result<GenerateBlueprintExamResponse>>;
