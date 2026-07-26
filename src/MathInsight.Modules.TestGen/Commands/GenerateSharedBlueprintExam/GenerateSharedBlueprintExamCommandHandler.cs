using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Commands.Common;
using MathInsight.Modules.TestGen.Commands.GenerateBlueprintExam;
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

namespace MathInsight.Modules.TestGen.Commands.GenerateSharedBlueprintExam;

public sealed class GenerateSharedBlueprintExamCommandHandler
    : IRequestHandler<GenerateSharedBlueprintExamCommand, Result<GenerateSharedBlueprintExamResponse>>
{
    private const int MaximumTestCodeAttempts = 5;

    private readonly TestGenDbContext _context;
    private readonly IBlueprintExamCandidateProvider _candidateProvider;
    private readonly IBlueprintExamQuestionSelector _selector;
    private readonly ITestCodeGenerator _testCodeGenerator;

    public GenerateSharedBlueprintExamCommandHandler(
        TestGenDbContext context,
        IBlueprintExamCandidateProvider candidateProvider,
        IBlueprintExamQuestionSelector selector,
        ITestCodeGenerator testCodeGenerator)
    {
        _context = context;
        _candidateProvider = candidateProvider;
        _selector = selector;
        _testCodeGenerator = testCodeGenerator;
    }

    public async Task<Result<GenerateSharedBlueprintExamResponse>> Handle(
        GenerateSharedBlueprintExamCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.ExpertId))
            return Result<GenerateSharedBlueprintExamResponse>.Failure(ApplicationErrors.AuthInvalidToken);
        if (string.IsNullOrWhiteSpace(command.BlueprintId) ||
            string.IsNullOrWhiteSpace(command.TestName) ||
            command.TestName.Trim().Length > 100 ||
            command.DurationMinutes <= 0)
        {
            return Result<GenerateSharedBlueprintExamResponse>.Failure(TestGenerationErrors.RequestInvalid);
        }

        var normalizedCommand = command with { TestName = command.TestName.Trim() };
        var testId = Guid.NewGuid().ToString("D");
        var createdTime = DateTime.UtcNow;

        for (var attempt = 0; attempt < MaximumTestCodeAttempts; attempt++)
        {
            var testCode = _testCodeGenerator.Generate();
            try
            {
                return await TestGenerationExecutionStrategy.ExecuteAsync(
                    _context,
                    () => ExecuteAsync(normalizedCommand, testId, testCode, createdTime, cancellationToken),
                    () => VerifySucceededAsync(normalizedCommand, testId, testCode, cancellationToken),
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
        GenerateSharedBlueprintExamCommand command,
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

        var existing = await _context.Tests
            .AsNoTracking()
            .Include(test => test.Questions)
            .FirstOrDefaultAsync(test => test.TestId == testId, cancellationToken);
        if (existing is not null)
        {
            var verification = await VerifySucceededAsync(
                command,
                testId,
                testCode,
                cancellationToken);
            return verification.IsSuccessful
                ? verification.Result
                : Result<GenerateSharedBlueprintExamResponse>.Failure(TestGenerationErrors.GenerationConflict);
        }

        if (await _context.Tests.AsNoTracking().AnyAsync(test => test.TestCode == testCode, cancellationToken))
            throw new TestCodeCollisionException();

        var expertExists = await _context.Experts
            .AsNoTracking()
            .AnyAsync(expert => expert.ExpertId == command.ExpertId, cancellationToken);
        if (!expertExists)
            return Result<GenerateSharedBlueprintExamResponse>.Failure(ApplicationErrors.AuthInvalidToken);

        var blueprint = await _context.Blueprints
            .Include(item => item.Sections)
                .ThenInclude(section => section.Details)
            .FirstOrDefaultAsync(item => item.BlueprintId == command.BlueprintId, cancellationToken);
        if (blueprint is null || blueprint.Status == BlueprintStatuses.Deactivated)
            return Result<GenerateSharedBlueprintExamResponse>.Failure(BlueprintErrors.NotFound);
        if (!string.Equals(blueprint.ExpertId, command.ExpertId, StringComparison.OrdinalIgnoreCase))
            return Result<GenerateSharedBlueprintExamResponse>.Failure(BlueprintErrors.MutationForbidden);
        if (blueprint.Status is not (BlueprintStatuses.Approved or BlueprintStatuses.Active))
            return Result<GenerateSharedBlueprintExamResponse>.Failure(BlueprintErrors.StatusInvalid);

        var requirements = BlueprintExamGenerationPlanner.BuildRequirements(blueprint);
        var structureError = BlueprintExamGenerationPlanner.ValidateStructure(blueprint, requirements);
        if (structureError == BlueprintExamStructureError.Invalid)
            return Result<GenerateSharedBlueprintExamResponse>.Failure(BlueprintErrors.StructureInvalid);
        if (structureError == BlueprintExamStructureError.ScoreBudgetMismatch)
            return Result<GenerateSharedBlueprintExamResponse>.Failure(TestGenerationErrors.ScoreBudgetMismatch);

        var candidatePool = await _candidateProvider.GetCandidatesAsync(blueprint, cancellationToken);
        var selection = _selector.Select(requirements, candidatePool.Candidates, cancellationToken);
        if (!selection.IsComplete || selection.Assignments.Count != blueprint.TotalQuestions)
        {
            var diagnosticCandidates = candidatePool.Candidates
                .Concat(candidatePool.InvalidVersionCandidates)
                .ToList();
            var versionDiagnostic = _selector.Select(requirements, diagnosticCandidates, cancellationToken);
            var error = versionDiagnostic.IsComplete
                ? TestGenerationErrors.QuestionVersionMissing
                : TestGenerationErrors.QuestionPoolInsufficient;
            return Result<GenerateSharedBlueprintExamResponse>.Failure(error);
        }

        var preparedQuestions = BlueprintExamGenerationPlanner.PrepareQuestions(
            blueprint,
            selection,
            candidatePool.Candidates);
        var test = CreateTest(
            blueprint,
            command,
            preparedQuestions,
            testId,
            testCode,
            createdTime);

        if (blueprint.Status == BlueprintStatuses.Approved)
            blueprint.Status = BlueprintStatuses.Active;

        _context.Tests.Add(test);
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return Result<GenerateSharedBlueprintExamResponse>.Success(ToResponse(test));
    }

    private async Task<(bool IsSuccessful, Result<GenerateSharedBlueprintExamResponse> Result)> VerifySucceededAsync(
        GenerateSharedBlueprintExamCommand command,
        string testId,
        string testCode,
        CancellationToken cancellationToken)
    {
        var test = await _context.Tests
            .AsNoTracking()
            .Include(item => item.Questions)
            .FirstOrDefaultAsync(item => item.TestId == testId, cancellationToken);
        if (test is null)
            return (false, default!);

        var blueprint = await _context.Blueprints
            .AsNoTracking()
            .Include(item => item.Sections)
                .ThenInclude(section => section.Details)
            .FirstOrDefaultAsync(item => item.BlueprintId == command.BlueprintId, cancellationToken);
        if (blueprint is null)
            return (false, default!);

        var expectedQuantities = blueprint.Sections
            .SelectMany(section => section.Details)
            .ToDictionary(detail => detail.BlueprintDetailId, detail => detail.Quantity, StringComparer.OrdinalIgnoreCase);
        var actualQuantities = test.Questions
            .Where(question => question.SourceBlueprintDetailId is not null)
            .GroupBy(question => question.SourceBlueprintDetailId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var quantitiesMatch = expectedQuantities.Count == actualQuantities.Count &&
            expectedQuantities.All(item => actualQuantities.TryGetValue(item.Key, out var count) && count == item.Value);
        var orders = test.Questions.Select(question => question.QuestionOrder).OrderBy(order => order);
        var succeeded = blueprint.Status == BlueprintStatuses.Active &&
            test.BlueprintId == command.BlueprintId &&
            test.TestCode == testCode &&
            test.TestStatus == GeneratedTestValues.ActiveStatus &&
            test.TestMode == GeneratedTestValues.BlueprintExamMode &&
            test.GeneratedForStudentId is null &&
            test.GeneratedBy == GeneratedTestValues.SystemGenerator &&
            test.TestName == command.TestName &&
            test.DurationMinutes == command.DurationMinutes &&
            test.TotalQuestions == test.Questions.Count &&
            test.MaxScore == blueprint.TotalScore &&
            test.ScoringPolicy == ScoringPolicies.BlueprintBudget &&
            test.Questions.Sum(question => question.MaxPointsSnapshot) == test.MaxScore &&
            orders.SequenceEqual(Enumerable.Range(1, test.TotalQuestions)) &&
            quantitiesMatch &&
            test.Questions.All(question =>
                !string.IsNullOrWhiteSpace(question.QuestionVersionId) &&
                question.WeightSnapshot > 0m &&
                question.MaxPointsSnapshot >= 0m &&
                ScoringRules.IsSupported(question.ScoringRuleSnapshot));

        return succeeded
            ? (true, Result<GenerateSharedBlueprintExamResponse>.Success(ToResponse(test)))
            : (false, default!);
    }

    private static TestEntity CreateTest(
        Blueprint blueprint,
        GenerateSharedBlueprintExamCommand command,
        IReadOnlyList<PreparedBlueprintExamQuestion> preparedQuestions,
        string testId,
        string testCode,
        DateTime createdTime)
    {
        var test = new TestEntity
        {
            TestId = testId,
            BlueprintId = blueprint.BlueprintId,
            TestStatus = GeneratedTestValues.ActiveStatus,
            TestMode = GeneratedTestValues.BlueprintExamMode,
            GeneratedForStudentId = null,
            GeneratedBy = GeneratedTestValues.SystemGenerator,
            TestName = command.TestName,
            TestCode = testCode,
            DurationMinutes = command.DurationMinutes,
            TotalQuestions = blueprint.TotalQuestions,
            MaxScore = blueprint.TotalScore,
            ScoringPolicy = ScoringPolicies.BlueprintBudget,
            CreatedTime = createdTime
        };

        foreach (var prepared in preparedQuestions)
        {
            test.Questions.Add(new TestQuestion
            {
                TestId = testId,
                QuestionId = prepared.Assignment.QuestionId,
                QuestionOrder = prepared.QuestionOrder,
                SourceBlueprintDetailId = prepared.Assignment.BlueprintDetailId,
                SelectionReason = GeneratedTestValues.BlueprintNormalReason,
                IsAdaptiveSelected = false,
                QuestionVersionId = prepared.Candidate.QuestionVersionId,
                WeightSnapshot = prepared.Candidate.DefaultWeight,
                MaxPointsSnapshot = prepared.MaxPoints,
                ScoringRuleSnapshot = prepared.ScoringRule,
                IsScoreInvalidated = false
            });
        }

        return test;
    }

    private static GenerateSharedBlueprintExamResponse ToResponse(TestEntity test)
        => new(
            test.TestId,
            test.BlueprintId!,
            test.TestCode!,
            test.TestMode,
            test.TestStatus,
            test.GeneratedBy,
            test.GeneratedForStudentId,
            test.TestName,
            test.DurationMinutes,
            test.TotalQuestions,
            test.MaxScore,
            test.ScoringPolicy,
            test.CreatedTime,
            test.Questions
                .OrderBy(question => question.QuestionOrder)
                .Select(question => new GeneratedTestQuestionResponse(
                    question.QuestionId,
                    question.QuestionVersionId,
                    question.QuestionOrder,
                    question.SourceBlueprintDetailId!,
                    question.WeightSnapshot,
                    question.MaxPointsSnapshot,
                    question.ScoringRuleSnapshot))
                .ToList());
}
