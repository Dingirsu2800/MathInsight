using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Commands.Common;
using MathInsight.Modules.TestGen.Commands.GenerateBlueprintExam;
using MathInsight.Modules.TestGen.Commands.GenerateSharedBlueprintExam;
using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Generation;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Shared.Results;
using MathInsight.Shared.Scoring;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TestEntity = MathInsight.Modules.TestGen.Persistence.Entities.Test;

namespace MathInsight.Modules.TestGen.Commands.GenerateFixedBlueprintExam;

public sealed class GenerateFixedBlueprintExamCommandHandler
    : IRequestHandler<GenerateFixedBlueprintExamCommand, Result<GenerateSharedBlueprintExamResponse>>
{
    private const int MaximumTestCodeAttempts = 5;
    private readonly TestGenDbContext _context;
    private readonly IBlueprintExamCandidateProvider _candidateProvider;
    private readonly ITestCodeGenerator _testCodeGenerator;

    public GenerateFixedBlueprintExamCommandHandler(
        TestGenDbContext context,
        IBlueprintExamCandidateProvider candidateProvider,
        ITestCodeGenerator testCodeGenerator)
    {
        _context = context;
        _candidateProvider = candidateProvider;
        _testCodeGenerator = testCodeGenerator;
    }

    public async Task<Result<GenerateSharedBlueprintExamResponse>> Handle(
        GenerateFixedBlueprintExamCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.ExpertId))
            return Result<GenerateSharedBlueprintExamResponse>.Failure(ApplicationErrors.AuthInvalidToken);
        if (string.IsNullOrWhiteSpace(command.BlueprintId) || string.IsNullOrWhiteSpace(command.TestName) ||
            command.TestName.Trim().Length > 100 || command.DurationMinutes <= 0 ||
            command.Questions is null || command.Questions.Count == 0)
            return Result<GenerateSharedBlueprintExamResponse>.Failure(TestGenerationErrors.RequestInvalid);

        var normalized = command with { TestName = command.TestName.Trim() };
        var testId = Guid.NewGuid().ToString("D");
        var createdTime = DateTime.UtcNow;
        for (var attempt = 0; attempt < MaximumTestCodeAttempts; attempt++)
        {
            var testCode = _testCodeGenerator.Generate();
            try
            {
                return await TestGenerationExecutionStrategy.ExecuteAsync(
                    _context,
                    () => ExecuteAsync(normalized, testId, testCode, createdTime, cancellationToken),
                    () => VerifySucceededAsync(normalized, testId, testCode, cancellationToken),
                    cancellationToken);
            }
            catch (TestCodeCollisionException)
            {
                _context.ChangeTracker.Clear();
            }
            catch (DbUpdateException exception) when (TestCodeCollisionDetector.IsTestCodeCollision(exception))
            {
                _context.ChangeTracker.Clear();
            }
        }

        return Result<GenerateSharedBlueprintExamResponse>.Failure(TestGenerationErrors.GenerationConflict);
    }

    private async Task<Result<GenerateSharedBlueprintExamResponse>> ExecuteAsync(
        GenerateFixedBlueprintExamCommand command,
        string testId,
        string testCode,
        DateTime createdTime,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction? transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        if (BlueprintSqlServerLock.IsSupported(_context))
            await BlueprintSqlServerLock.LockAsync(_context, command.BlueprintId, cancellationToken);

        if (await _context.Tests.AsNoTracking().AnyAsync(x => x.TestId == testId, cancellationToken))
        {
            var verification = await VerifySucceededAsync(command, testId, testCode, cancellationToken);
            return verification.IsSuccessful
                ? verification.Result
                : Result<GenerateSharedBlueprintExamResponse>.Failure(TestGenerationErrors.GenerationConflict);
        }
        if (await _context.Tests.AsNoTracking().AnyAsync(x => x.TestCode == testCode, cancellationToken))
            throw new TestCodeCollisionException();
        if (!await _context.Experts.AsNoTracking().AnyAsync(x => x.ExpertId == command.ExpertId, cancellationToken))
            return Result<GenerateSharedBlueprintExamResponse>.Failure(ApplicationErrors.AuthInvalidToken);

        var blueprint = await _context.Blueprints
            .Include(x => x.Sections).ThenInclude(x => x.Details)
            .FirstOrDefaultAsync(x => x.BlueprintId == command.BlueprintId, cancellationToken);
        if (blueprint is null || blueprint.Status == BlueprintStatuses.Deactivated)
            return Result<GenerateSharedBlueprintExamResponse>.Failure(BlueprintErrors.NotFound);
        if (!string.Equals(blueprint.ExpertId, command.ExpertId, StringComparison.OrdinalIgnoreCase))
            return Result<GenerateSharedBlueprintExamResponse>.Failure(BlueprintErrors.MutationForbidden);
        if (blueprint.Status is not (BlueprintStatuses.Approved or BlueprintStatuses.Active))
            return Result<GenerateSharedBlueprintExamResponse>.Failure(TestGenerationErrors.FixedTestBlueprintNotApproved);

        var requirements = BlueprintExamGenerationPlanner.BuildRequirements(blueprint);
        var structureError = BlueprintExamGenerationPlanner.ValidateStructure(blueprint, requirements);
        if (structureError == BlueprintExamStructureError.Invalid)
            return Result<GenerateSharedBlueprintExamResponse>.Failure(BlueprintErrors.StructureInvalid);
        if (structureError == BlueprintExamStructureError.ScoreBudgetMismatch)
            return Result<GenerateSharedBlueprintExamResponse>.Failure(TestGenerationErrors.ScoreBudgetMismatch);

        var requested = command.Questions.Select(x => new FixedBlueprintExamQuestionSelection(
            x.QuestionId, x.BlueprintDetailId, x.QuestionOrder)).ToList();
        var candidatePool = await _candidateProvider.GetCandidatesAsync(blueprint, cancellationToken);
        var allCandidates = candidatePool.Candidates.Concat(candidatePool.InvalidVersionCandidates).ToList();
        var selection = FixedBlueprintExamPlanner.Select(requirements, allCandidates, requested);
        if (selection.Error != FixedBlueprintExamSelectionError.None)
            return Result<GenerateSharedBlueprintExamResponse>.Failure(MapSelectionError(selection.Error));

        var prepared = FixedBlueprintExamPlanner.PrepareQuestions(blueprint, selection.Selection, candidatePool.Candidates);
        var test = CreateTest(blueprint, command, prepared, testId, testCode, createdTime);
        if (blueprint.Status == BlueprintStatuses.Approved)
            blueprint.Status = BlueprintStatuses.Active;
        _context.Tests.Add(test);
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        return Result<GenerateSharedBlueprintExamResponse>.Success(ToResponse(test));
    }

    private async Task<(bool IsSuccessful, Result<GenerateSharedBlueprintExamResponse> Result)> VerifySucceededAsync(
        GenerateFixedBlueprintExamCommand command,
        string testId,
        string testCode,
        CancellationToken cancellationToken)
    {
        var test = await _context.Tests.AsNoTracking().Include(x => x.Questions)
            .FirstOrDefaultAsync(x => x.TestId == testId, cancellationToken);
        var expected = command.Questions.OrderBy(x => x.QuestionOrder).ToList();
        var actual = test?.Questions.OrderBy(x => x.QuestionOrder).ToList();
        var succeeded = test is not null && test.TestCode == testCode &&
            test.GeneratedBy == GeneratedTestValues.ExpertGenerator &&
            test.Questions.Count == expected.Count && actual is not null &&
            expected.Zip(actual).All(pair =>
                pair.First.QuestionId == pair.Second.QuestionId &&
                pair.First.BlueprintDetailId == pair.Second.SourceBlueprintDetailId &&
                pair.First.QuestionOrder == pair.Second.QuestionOrder &&
                pair.Second.SelectionReason == GeneratedTestValues.FixedExamReason);
        return succeeded
            ? (true, Result<GenerateSharedBlueprintExamResponse>.Success(ToResponse(test!)))
            : (false, default!);
    }

    private static TestEntity CreateTest(
        Blueprint blueprint,
        GenerateFixedBlueprintExamCommand command,
        IReadOnlyList<PreparedBlueprintExamQuestion> prepared,
        string testId,
        string testCode,
        DateTime createdTime)
    {
        var test = new TestEntity
        {
            TestId = testId, BlueprintId = blueprint.BlueprintId,
            TestStatus = GeneratedTestValues.ActiveStatus, TestMode = GeneratedTestValues.BlueprintExamMode,
            GeneratedForStudentId = null, GeneratedBy = GeneratedTestValues.ExpertGenerator,
            TestName = command.TestName, TestCode = testCode, DurationMinutes = command.DurationMinutes,
            TotalQuestions = blueprint.TotalQuestions, MaxScore = blueprint.TotalScore,
            ScoringPolicy = ScoringPolicies.BlueprintBudget, CreatedTime = createdTime
        };
        foreach (var item in prepared)
        {
            test.Questions.Add(new TestQuestion
            {
                TestId = testId, QuestionId = item.Assignment.QuestionId,
                QuestionOrder = item.QuestionOrder, SourceBlueprintDetailId = item.Assignment.BlueprintDetailId,
                SelectionReason = GeneratedTestValues.FixedExamReason, IsAdaptiveSelected = false,
                QuestionVersionId = item.Candidate.QuestionVersionId, WeightSnapshot = item.Candidate.DefaultWeight,
                MaxPointsSnapshot = item.MaxPoints, ScoringRuleSnapshot = item.ScoringRule,
                IsScoreInvalidated = false
            });
        }
        return test;
    }

    private static Error MapSelectionError(FixedBlueprintExamSelectionError error) => error switch
    {
        FixedBlueprintExamSelectionError.DuplicateQuestion => TestGenerationErrors.FixedTestQuestionDuplicated,
        FixedBlueprintExamSelectionError.InvalidOrder => TestGenerationErrors.FixedTestOrderInvalid,
        FixedBlueprintExamSelectionError.DetailQuantityMismatch => TestGenerationErrors.FixedTestDetailQuantityMismatch,
        FixedBlueprintExamSelectionError.QuestionVersionUnavailable => TestGenerationErrors.FixedTestQuestionVersionUnavailable,
        _ => TestGenerationErrors.FixedTestQuestionNotEligible
    };

    private static GenerateSharedBlueprintExamResponse ToResponse(TestEntity test) => new(
        test.TestId, test.BlueprintId!, test.TestCode!, test.TestMode, test.TestStatus,
        test.GeneratedBy, test.GeneratedForStudentId, test.TestName, test.DurationMinutes,
        test.TotalQuestions, test.MaxScore, test.ScoringPolicy, test.CreatedTime,
        test.Questions.OrderBy(x => x.QuestionOrder).Select(x => new GeneratedTestQuestionResponse(
            x.QuestionId, x.QuestionVersionId, x.QuestionOrder, x.SourceBlueprintDetailId!,
            x.WeightSnapshot, x.MaxPointsSnapshot, x.ScoringRuleSnapshot)).ToList());
}
