using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.TestGen.Queries.GetBlueprintExamOptions;

public sealed class GetBlueprintExamOptionsQueryHandler
    : IRequestHandler<GetBlueprintExamOptionsQuery, Result<IReadOnlyList<BlueprintExamOptionResponse>>>
{
    private readonly TestGenDbContext _context;

    public GetBlueprintExamOptionsQueryHandler(TestGenDbContext context)
    {
        _context = context;
    }

    public async Task<Result<IReadOnlyList<BlueprintExamOptionResponse>>> Handle(
        GetBlueprintExamOptionsQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.StudentId))
        {
            return Result<IReadOnlyList<BlueprintExamOptionResponse>>.Failure(
                ApplicationErrors.AuthInvalidToken);
        }

        var grade = await _context.Students
            .AsNoTracking()
            .Where(student => student.StudentId == query.StudentId)
            .Select(student => student.CurrentGrade)
            .FirstOrDefaultAsync(cancellationToken);

        if (grade is not (10 or 11 or 12))
        {
            return Result<IReadOnlyList<BlueprintExamOptionResponse>>.Failure(
                TestGenerationErrors.StudentNotFound);
        }

        var items = await _context.Blueprints
            .AsNoTracking()
            .Where(blueprint =>
                blueprint.Grade == grade &&
                (blueprint.Status == BlueprintStatuses.Approved ||
                 blueprint.Status == BlueprintStatuses.Active))
            .OrderBy(blueprint => blueprint.BlueprintName)
            .ThenBy(blueprint => blueprint.BlueprintId)
            .Select(blueprint => new BlueprintExamOptionResponse(
                blueprint.BlueprintId,
                blueprint.BlueprintName,
                blueprint.Grade,
                blueprint.TotalQuestions,
                blueprint.TotalScore,
                blueprint.DurationMinutes,
                blueprint.Status,
                blueprint.Sections.Count))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<BlueprintExamOptionResponse>>.Success(items);
    }
}
