using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.TestGen.Commands.ArchiveSharedBlueprintExam;

public sealed record ArchiveSharedBlueprintExamCommand(
    string TestId,
    string ExpertId,
    string Status) : IRequest<Result<UpdateGeneratedTestStatusResponse>>;
