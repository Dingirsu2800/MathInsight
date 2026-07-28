using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Generation;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.TestGen.Queries.GetSharedBlueprintExams;

public sealed class GetSharedBlueprintExamsQueryHandler
    : IRequestHandler<GetSharedBlueprintExamsQuery, Result<PagedSharedBlueprintExamResponse>>
{
    private readonly TestGenDbContext _context;

    public GetSharedBlueprintExamsQueryHandler(TestGenDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedSharedBlueprintExamResponse>> Handle(
        GetSharedBlueprintExamsQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.StudentId))
            return Result<PagedSharedBlueprintExamResponse>.Failure(ApplicationErrors.AuthInvalidToken);

        var grade = await _context.Students
            .AsNoTracking()
            .Where(student => student.StudentId == query.StudentId)
            .Select(student => student.CurrentGrade)
            .FirstOrDefaultAsync(cancellationToken);
        if (grade is not (10 or 11 or 12))
            return Result<PagedSharedBlueprintExamResponse>.Failure(TestGenerationErrors.StudentNotFound);

        var pageIndex = query.PageIndex <= 0 ? 1 : query.PageIndex;
        var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);
        pageIndex = Math.Min(pageIndex, int.MaxValue / pageSize);
        var source = _context.Tests
            .AsNoTracking()
            .Where(test =>
                test.TestStatus == GeneratedTestValues.ActiveStatus &&
                test.TestMode == GeneratedTestValues.BlueprintExamMode &&
                test.GeneratedForStudentId == null &&
                test.Blueprint != null &&
                test.Blueprint.Status == BlueprintStatuses.Active &&
                test.Blueprint.Grade == grade);
        var totalCount = await source.CountAsync(cancellationToken);
        var items = await source
            .OrderByDescending(test => test.CreatedTime)
            .ThenBy(test => test.TestId)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(test => new SharedBlueprintExamResponse(
                test.TestId,
                test.BlueprintId!,
                test.TestName,
                test.TestCode,
                test.Blueprint!.Grade,
                test.DurationMinutes,
                test.TotalQuestions,
                test.MaxScore,
                test.CreatedTime))
            .ToListAsync(cancellationToken);

        return Result<PagedSharedBlueprintExamResponse>.Success(
            new(pageIndex, pageSize, totalCount, items));
    }
}
