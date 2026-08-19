using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.TestGen.Queries.GetBlueprintExamOptions;

public sealed class GetBlueprintExamOptionsQueryHandler
    : IRequestHandler<GetBlueprintExamOptionsQuery, Result<BlueprintExamOptionsResponse>>
{
    private const int MaxPageSize = 50;

    private readonly TestGenDbContext _context;

    public GetBlueprintExamOptionsQueryHandler(TestGenDbContext context)
    {
        _context = context;
    }

    public async Task<Result<BlueprintExamOptionsResponse>> Handle(
        GetBlueprintExamOptionsQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.StudentId))
        {
            return Result<BlueprintExamOptionsResponse>.Failure(
                ApplicationErrors.AuthInvalidToken);
        }

        if (query.PageIndex < 1 || query.PageSize is < 1 or > MaxPageSize)
        {
            return Result<BlueprintExamOptionsResponse>.Failure(
                TestGenerationErrors.RequestInvalid);
        }

        var grade = await _context.Students
            .AsNoTracking()
            .Where(student => student.StudentId == query.StudentId)
            .Select(student => student.CurrentGrade)
            .FirstOrDefaultAsync(cancellationToken);

        if (grade is not (10 or 11 or 12))
        {
            return Result<BlueprintExamOptionsResponse>.Failure(
                TestGenerationErrors.StudentNotFound);
        }

        var blueprints = _context.Blueprints
            .AsNoTracking()
            .Where(blueprint =>
                blueprint.Grade == grade &&
                (blueprint.Status == BlueprintStatuses.Approved ||
                 blueprint.Status == BlueprintStatuses.Active))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            blueprints = blueprints.Where(blueprint => blueprint.BlueprintName.ToLower().Contains(search.ToLower()));
        }

        var totalCount = await blueprints.CountAsync(cancellationToken);
        var skip = (long)(query.PageIndex - 1) * query.PageSize;
        var items = skip > int.MaxValue
            ? []
            : await blueprints
            .OrderByDescending(blueprint => blueprint.ReviewTime)
            .ThenBy(blueprint => blueprint.BlueprintName)
            .ThenBy(blueprint => blueprint.BlueprintId)
            .Skip((int)skip)
            .Take(query.PageSize)
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

        return Result<BlueprintExamOptionsResponse>.Success(
            new BlueprintExamOptionsResponse(
                items,
                totalCount,
                query.PageIndex,
                query.PageSize));
    }
}
