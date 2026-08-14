using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Generation;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.TestGen.Queries.ResolveSharedTestCode;

public sealed class ResolveSharedTestCodeQueryHandler
    : IRequestHandler<ResolveSharedTestCodeQuery, Result<SharedBlueprintExamResponse>>
{
    private readonly TestGenDbContext _context;

    public ResolveSharedTestCodeQueryHandler(TestGenDbContext context)
    {
        _context = context;
    }

    public async Task<Result<SharedBlueprintExamResponse>> Handle(
        ResolveSharedTestCodeQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.StudentId))
            return Result<SharedBlueprintExamResponse>.Failure(ApplicationErrors.AuthInvalidToken);
        if (string.IsNullOrWhiteSpace(query.TestCode))
            return Result<SharedBlueprintExamResponse>.Failure(TestGenerationErrors.RequestInvalid);

        var grade = await _context.Students
            .AsNoTracking()
            .Where(student => student.StudentId == query.StudentId)
            .Select(student => student.CurrentGrade)
            .FirstOrDefaultAsync(cancellationToken);
        if (grade is not (10 or 11 or 12))
            return Result<SharedBlueprintExamResponse>.Failure(TestGenerationErrors.StudentNotFound);

        var normalizedCode = query.TestCode.Trim().ToUpperInvariant();
        var result = await _context.Tests
            .AsNoTracking()
            .Where(test =>
                test.TestCode == normalizedCode &&
                test.TestStatus == GeneratedTestValues.ActiveStatus &&
                test.TestMode == GeneratedTestValues.BlueprintExamMode &&
                test.GeneratedForStudentId == null &&
                test.Blueprint != null &&
                test.Blueprint.Status == BlueprintStatuses.Active &&
                test.Blueprint.Grade == grade)
            .Select(test => new SharedBlueprintExamResponse(
                test.TestId,
                test.BlueprintId!,
                test.TestName,
                test.TestCode,
                test.Questions.Any(question => question.SelectionReason == GeneratedTestValues.FixedExamReason)
                    ? GeneratedTestValues.FixedGenerationType
                    : GeneratedTestValues.RandomGenerationType,
                test.Blueprint!.Grade,
                test.DurationMinutes,
                test.TotalQuestions,
                test.MaxScore,
                test.CreatedTime))
            .FirstOrDefaultAsync(cancellationToken);

        return result is null
            ? Result<SharedBlueprintExamResponse>.Failure(TestGenerationErrors.TestCodeNotAvailable)
            : Result<SharedBlueprintExamResponse>.Success(result);
    }
}
