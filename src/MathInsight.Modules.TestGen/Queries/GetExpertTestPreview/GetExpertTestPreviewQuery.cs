using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.TestGen.Queries.GetExpertTestPreview;

public sealed record GetExpertTestPreviewQuery(
    string TestId,
    string ExpertId) : IRequest<Result<ExpertTestPreviewResponse>>;
