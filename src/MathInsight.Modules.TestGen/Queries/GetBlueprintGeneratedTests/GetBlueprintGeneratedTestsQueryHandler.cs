using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Generation;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.TestGen.Queries.GetBlueprintGeneratedTests;

public sealed class GetBlueprintGeneratedTestsQueryHandler
    : IRequestHandler<GetBlueprintGeneratedTestsQuery, Result<PagedExpertGeneratedTestResponse>>
{
    private readonly TestGenDbContext _context;

    public GetBlueprintGeneratedTestsQueryHandler(TestGenDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedExpertGeneratedTestResponse>> Handle(
        GetBlueprintGeneratedTestsQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.BlueprintId) || string.IsNullOrWhiteSpace(query.ExpertId))
            return Result<PagedExpertGeneratedTestResponse>.Failure(TestGenerationErrors.RequestInvalid);

        var ownerId = await _context.Blueprints
            .AsNoTracking()
            .Where(blueprint => blueprint.BlueprintId == query.BlueprintId)
            .Select(blueprint => blueprint.ExpertId)
            .FirstOrDefaultAsync(cancellationToken);
        if (ownerId is null)
            return Result<PagedExpertGeneratedTestResponse>.Failure(BlueprintErrors.NotFound);
        if (!string.Equals(ownerId, query.ExpertId, StringComparison.OrdinalIgnoreCase))
            return Result<PagedExpertGeneratedTestResponse>.Failure(BlueprintErrors.MutationForbidden);

        var pageIndex = query.PageIndex <= 0 ? 1 : query.PageIndex;
        var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);
        pageIndex = Math.Min(pageIndex, int.MaxValue / pageSize);

        var source = _context.Tests
            .AsNoTracking()
            .Where(test =>
                test.BlueprintId == query.BlueprintId &&
                test.TestMode == GeneratedTestValues.BlueprintExamMode &&
                test.GeneratedForStudentId == null &&
                test.TestCode != null);
        var totalCount = await source.CountAsync(cancellationToken);
        var items = await source
            .OrderByDescending(test => test.CreatedTime)
            .ThenBy(test => test.TestId)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(test => new ExpertGeneratedTestListItemResponse(
                test.TestId,
                test.BlueprintId!,
                test.TestName,
                test.TestCode!,
                test.TestStatus,
                GeneratedTestValues.ToGenerationType(
                    test.Questions.Any(question => question.SelectionReason == GeneratedTestValues.FixedExamReason)),
                test.DurationMinutes,
                test.TotalQuestions,
                test.MaxScore,
                test.CreatedTime))
            .ToListAsync(cancellationToken);

        return Result<PagedExpertGeneratedTestResponse>.Success(
            new(pageIndex, pageSize, totalCount, items));
    }
}
