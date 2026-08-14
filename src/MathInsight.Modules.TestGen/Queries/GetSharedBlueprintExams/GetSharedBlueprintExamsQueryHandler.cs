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
        var requestedGenerationType = NormalizeGenerationType(query.GenerationType);
        if (requestedGenerationType is null && !string.IsNullOrWhiteSpace(query.GenerationType))
            return Result<PagedSharedBlueprintExamResponse>.Failure(TestGenerationErrors.RequestInvalid);

        var source = _context.Tests
            .AsNoTracking()
            .Where(test =>
                test.TestStatus == GeneratedTestValues.ActiveStatus &&
                test.TestMode == GeneratedTestValues.BlueprintExamMode &&
                test.GeneratedForStudentId == null &&
                test.Blueprint != null &&
                test.Blueprint.Status == BlueprintStatuses.Active &&
                test.Blueprint.Grade == grade);

        var hasInvalidGenerationMetadata = await source.AnyAsync(test =>
            !test.Questions.Any() ||
            test.Questions.Any(question =>
                question.SelectionReason != GeneratedTestValues.FixedExamReason &&
                question.SelectionReason != GeneratedTestValues.BlueprintNormalReason) ||
            (test.Questions.Any(question => question.SelectionReason == GeneratedTestValues.FixedExamReason) &&
             test.Questions.Any(question => question.SelectionReason == GeneratedTestValues.BlueprintNormalReason)),
            cancellationToken);
        if (hasInvalidGenerationMetadata)
            return Result<PagedSharedBlueprintExamResponse>.Failure(TestGenerationErrors.SharedExamGenerationTypeInvalid);

        var classifiedSource = source.Select(test => new
        {
            Test = test,
            IsFixed = test.Questions.Any(question => question.SelectionReason == GeneratedTestValues.FixedExamReason)
        });
        if (requestedGenerationType == GeneratedTestValues.FixedGenerationType)
            classifiedSource = classifiedSource.Where(item => item.IsFixed);
        else if (requestedGenerationType == GeneratedTestValues.RandomGenerationType)
            classifiedSource = classifiedSource.Where(item => !item.IsFixed);

        var totalCount = await classifiedSource.CountAsync(cancellationToken);
        var items = await classifiedSource
            .OrderByDescending(item => item.Test.CreatedTime)
            .ThenBy(item => item.Test.TestId)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new SharedBlueprintExamResponse(
                item.Test.TestId,
                item.Test.BlueprintId!,
                item.Test.TestName,
                item.Test.TestCode,
                item.IsFixed ? GeneratedTestValues.FixedGenerationType : GeneratedTestValues.RandomGenerationType,
                item.Test.Blueprint!.Grade,
                item.Test.DurationMinutes,
                item.Test.TotalQuestions,
                item.Test.MaxScore,
                item.Test.CreatedTime))
            .ToListAsync(cancellationToken);

        return Result<PagedSharedBlueprintExamResponse>.Success(
            new(pageIndex, pageSize, totalCount, items));
    }

    private static string? NormalizeGenerationType(string? generationType)
    {
        if (string.IsNullOrWhiteSpace(generationType))
            return null;

        return generationType.Trim().ToUpperInvariant() switch
        {
            "FIXED" => GeneratedTestValues.FixedGenerationType,
            "RANDOM" => GeneratedTestValues.RandomGenerationType,
            _ => null
        };
    }
}
