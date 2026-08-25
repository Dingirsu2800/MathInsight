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

        var source = await _context.Tests
            .AsNoTracking()
            .Where(test =>
                test.TestStatus == GeneratedTestValues.ActiveStatus &&
                test.TestMode == GeneratedTestValues.BlueprintExamMode &&
                test.GeneratedForStudentId == null &&
                test.Blueprint != null &&
                test.Blueprint.Status == BlueprintStatuses.Active &&
                test.Blueprint.Grade == grade &&
                !test.Questions.Any(question => question.IsScoreInvalidated))
            .Select(test => new
            {
                test.TestId,
                test.BlueprintId,
                test.TestName,
                test.TestCode,
                Grade = test.Blueprint!.Grade,
                test.DurationMinutes,
                test.TotalQuestions,
                test.MaxScore,
                test.CreatedTime,
                SelectionReasons = test.Questions.Select(question => question.SelectionReason).ToList()
            })
            .ToListAsync(cancellationToken);

        var classifiedSource = new List<ClassifiedSharedBlueprintExam>();
        foreach (var test in source)
        {
            if (!SharedBlueprintExamGenerationTypeClassifier.TryClassify(test.SelectionReasons, out var generationType))
                return Result<PagedSharedBlueprintExamResponse>.Failure(TestGenerationErrors.SharedExamGenerationTypeInvalid);
            classifiedSource.Add(new ClassifiedSharedBlueprintExam(
                test.TestId,
                test.BlueprintId!,
                test.TestName,
                test.TestCode,
                generationType,
                test.Grade,
                test.DurationMinutes,
                test.TotalQuestions,
                test.MaxScore,
                test.CreatedTime));
        }

        var filteredSource = classifiedSource
            .Where(item => requestedGenerationType is null || item.GenerationType == requestedGenerationType)
            .OrderByDescending(item => item.CreatedTime)
            .ThenBy(item => item.TestId);
        var totalCount = filteredSource.Count();
        var items = filteredSource
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new SharedBlueprintExamResponse(
                item.TestId,
                item.BlueprintId,
                item.TestName,
                item.TestCode,
                item.GenerationType,
                item.Grade,
                item.DurationMinutes,
                item.TotalQuestions,
                item.MaxScore,
                item.CreatedTime))
            .ToList();

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

    private sealed record ClassifiedSharedBlueprintExam(
        string TestId,
        string BlueprintId,
        string TestName,
        string? TestCode,
        string GenerationType,
        int Grade,
        int DurationMinutes,
        int TotalQuestions,
        decimal MaxScore,
        DateTime CreatedTime);
}
