using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.TestGen.Commands.GenerateTopicPractice;

public sealed record GenerateTopicPracticeCommand(
    string StudentId,
    string TagId,
    string? DifficultyId = null) : IRequest<Result<GenerateTopicPracticeResponse>>;
