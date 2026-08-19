using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Commands.Common;
using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Generation;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Shared.Recommendations;
using MathInsight.Shared.Results;
using MathInsight.Shared.Scoring;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TestEntity = MathInsight.Modules.TestGen.Persistence.Entities.Test;

namespace MathInsight.Modules.TestGen.Commands.GenerateBlueprintExam;

public sealed class GenerateBlueprintExamCommandHandler
    : IRequestHandler<GenerateBlueprintExamCommand, Result<GenerateBlueprintExamResponse>>
{
    private readonly TestGenDbContext _context;
    private readonly IBlueprintExamCandidateProvider _candidateProvider;
    private readonly IBlueprintExamQuestionSelector _selector;
    private readonly IAdaptiveBlueprintExamQuestionSelector _adaptiveSelector;
    private readonly IStudentTopicMasteryProvider? _masteryProvider;
    private readonly bool _masteryAware;

    public GenerateBlueprintExamCommandHandler(
        TestGenDbContext context,
        IBlueprintExamCandidateProvider candidateProvider,
        IBlueprintExamQuestionSelector selector,
        IAdaptiveBlueprintExamQuestionSelector? adaptiveSelector = null,
        IStudentTopicMasteryProvider? masteryProvider = null)
    {
        _context = context;
        _candidateProvider = candidateProvider;
        _selector = selector;
        _adaptiveSelector = adaptiveSelector ?? new AdaptiveBlueprintExamQuestionSelector(new SystemGenerationRandomizer());
        _masteryProvider = masteryProvider;
        _masteryAware = adaptiveSelector is not null && masteryProvider is not null;
    }

    public async Task<Result<GenerateBlueprintExamResponse>> Handle(
        GenerateBlueprintExamCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.StudentId))
            return Result<GenerateBlueprintExamResponse>.Failure(ApplicationErrors.AuthInvalidToken);

        if (string.IsNullOrWhiteSpace(command.BlueprintId))
            return Result<GenerateBlueprintExamResponse>.Failure(TestGenerationErrors.RequestInvalid);

        var testId = Guid.NewGuid().ToString();
        var createdTime = DateTime.UtcNow;

        return await TestGenerationExecutionStrategy.ExecuteAsync(
            _context,
            () => ExecuteAsync(command, testId, createdTime, cancellationToken),
            () => VerifySucceededAsync(command, testId, cancellationToken),
            cancellationToken);
    }

    private async Task<Result<GenerateBlueprintExamResponse>> ExecuteAsync(
        GenerateBlueprintExamCommand command,
        string testId,
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
            return Result<GenerateBlueprintExamResponse>.Success(ToResponse(existing));

        var studentGrade = await _context.Students
            .AsNoTracking()
            .Where(student => student.StudentId == command.StudentId)
            .Select(student => student.CurrentGrade)
            .FirstOrDefaultAsync(cancellationToken);
        if (studentGrade is not (10 or 11 or 12))
            return Result<GenerateBlueprintExamResponse>.Failure(TestGenerationErrors.StudentNotFound);

        var blueprint = await _context.Blueprints
            .Include(item => item.Sections)
                .ThenInclude(section => section.Details)
            .FirstOrDefaultAsync(
                item => item.BlueprintId == command.BlueprintId,
                cancellationToken);

        if (blueprint is null || blueprint.Status == BlueprintStatuses.Deactivated)
            return Result<GenerateBlueprintExamResponse>.Failure(TestGenerationErrors.BlueprintNotFound);

        if (blueprint.Status is not (BlueprintStatuses.Approved or BlueprintStatuses.Active))
            return Result<GenerateBlueprintExamResponse>.Failure(TestGenerationErrors.BlueprintUnavailable);

        if (blueprint.Grade != studentGrade)
            return Result<GenerateBlueprintExamResponse>.Failure(TestGenerationErrors.GradeMismatch);

        var requirements = BlueprintExamGenerationPlanner.BuildRequirements(blueprint);
        if (BlueprintExamGenerationPlanner.ValidateStructure(blueprint, requirements) != BlueprintExamStructureError.None)
            return Result<GenerateBlueprintExamResponse>.Failure(TestGenerationErrors.BlueprintUnavailable);

        IReadOnlyDictionary<string, AdaptiveBlueprintDetailPlan> plansByDetailId =
            new Dictionary<string, AdaptiveBlueprintDetailPlan>(StringComparer.OrdinalIgnoreCase);
        BlueprintExamCandidatePool candidatePool;
        BlueprintExamSelection selection;

        if (_masteryAware)
        {
            var resolution = await ResolveAdaptivePlansAsync(
                command.StudentId,
                requirements,
                cancellationToken);
            if (resolution.IsFailure)
                return Result<GenerateBlueprintExamResponse>.Failure(resolution.Error!);

            plansByDetailId = resolution.Value!.PlansByDetailId;
            candidatePool = await _candidateProvider.GetCandidatesAsync(
                blueprint,
                resolution.Value.DifficultyIds,
                cancellationToken);
            selection = _adaptiveSelector.Select(
                requirements,
                plansByDetailId,
                candidatePool.Candidates,
                cancellationToken);
        }
        else
        {
            candidatePool = await _candidateProvider.GetCandidatesAsync(blueprint, cancellationToken);
            selection = _selector.Select(requirements, candidatePool.Candidates, cancellationToken);
        }

        if (!selection.IsComplete || selection.Assignments.Count != blueprint.TotalQuestions)
            return Result<GenerateBlueprintExamResponse>.Failure(TestGenerationErrors.InsufficientQuestions);

        var preparedQuestions = BlueprintExamGenerationPlanner.PrepareQuestions(
            blueprint,
            selection,
            candidatePool.Candidates);

        var test = new TestEntity
        {
            TestId = testId,
            BlueprintId = blueprint.BlueprintId,
            TestStatus = GeneratedTestValues.ActiveStatus,
            TestMode = GeneratedTestValues.BlueprintExamMode,
            GeneratedForStudentId = command.StudentId,
            GeneratedBy = GeneratedTestValues.SystemGenerator,
            TestName = blueprint.BlueprintName,
            TestCode = null,
            DurationMinutes = blueprint.DurationMinutes,
            TotalQuestions = blueprint.TotalQuestions,
            MaxScore = blueprint.TotalScore,
            ScoringPolicy = ScoringPolicies.BlueprintBudget,
            CreatedTime = createdTime
        };

        foreach (var prepared in preparedQuestions)
        {
            var audit = ResolveAudit(prepared, plansByDetailId);
            test.Questions.Add(new TestQuestion
            {
                TestId = test.TestId,
                QuestionId = prepared.Assignment.QuestionId,
                QuestionOrder = prepared.QuestionOrder,
                SourceBlueprintDetailId = prepared.Assignment.BlueprintDetailId,
                SelectionReason = GeneratedTestValues.BlueprintNormalReason,
                IsAdaptiveSelected = audit.IsAdaptive,
                RecommendedForTagId = audit.RecommendedForTagId,
                RecommendedDifficultyId = audit.RecommendedDifficultyId,
                PtagAtSelection = audit.PtagAtSelection,
                RuleVersion = audit.RuleVersion,
                QuestionVersionId = prepared.Candidate.QuestionVersionId,
                WeightSnapshot = prepared.Candidate.DefaultWeight,
                MaxPointsSnapshot = prepared.MaxPoints,
                ScoringRuleSnapshot = prepared.ScoringRule,
                IsScoreInvalidated = false,
                InvalidatedByReportId = null
            });
        }

        if (blueprint.Status == BlueprintStatuses.Approved)
            blueprint.Status = BlueprintStatuses.Active;

        _context.Tests.Add(test);
        await _context.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return Result<GenerateBlueprintExamResponse>.Success(ToResponse(test));
    }

    private async Task<Result<AdaptiveBlueprintResolution>> ResolveAdaptivePlansAsync(
        string studentId,
        IReadOnlyList<BlueprintExamRequirement> requirements,
        CancellationToken cancellationToken)
    {
        if (_masteryProvider is null)
            return Result<AdaptiveBlueprintResolution>.Failure(TestGenerationErrors.AdaptiveExamMasteryUnavailable);

        var tagIds = requirements
            .Select(requirement => requirement.TagId)
            .Where(tagId => !string.IsNullOrWhiteSpace(tagId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        IReadOnlyDictionary<string, TopicMasteryAdvice> advice;
        try
        {
            advice = await _masteryProvider.GetTopicMasteryAdviceAsync(
                studentId,
                tagIds,
                cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<AdaptiveBlueprintResolution>.Failure(TestGenerationErrors.AdaptiveExamMasteryUnavailable);
        }
        catch (Exception)
        {
            return Result<AdaptiveBlueprintResolution>.Failure(TestGenerationErrors.AdaptiveExamMasteryUnavailable);
        }

        if (advice is null)
            return Result<AdaptiveBlueprintResolution>.Failure(TestGenerationErrors.AdaptiveExamMasteryInvalid);

        var adviceByTag = new Dictionary<string, TopicMasteryAdvice>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in advice)
        {
            if (!tagIds.Contains(entry.Key, StringComparer.OrdinalIgnoreCase))
                continue;

            if (entry.Value is null ||
                string.IsNullOrWhiteSpace(entry.Value.TagId) ||
                !string.Equals(entry.Key, entry.Value.TagId, StringComparison.OrdinalIgnoreCase) ||
                entry.Value.OfficialPoint < 0m ||
                entry.Value.OfficialPoint > 10m ||
                entry.Value.EvidenceCount < 0 ||
                !adviceByTag.TryAdd(entry.Key, entry.Value))
            {
                return Result<AdaptiveBlueprintResolution>.Failure(TestGenerationErrors.AdaptiveExamMasteryInvalid);
            }
        }

        var activeDifficulties = await _context.TagDifficulties
            .AsNoTracking()
            .Where(difficulty =>
                difficulty.IsActive &&
                difficulty.LevelValue >= 1 &&
                difficulty.LevelValue <= 4)
            .OrderBy(difficulty => difficulty.DisplayOrder)
            .ThenBy(difficulty => difficulty.DifficultyId)
            .ToListAsync(cancellationToken);
        var difficultyById = activeDifficulties.ToDictionary(
            difficulty => difficulty.DifficultyId,
            StringComparer.OrdinalIgnoreCase);
        var difficultyByLevel = activeDifficulties
            .GroupBy(difficulty => difficulty.LevelValue)
            .ToDictionary(group => group.Key, group => group.First());

        var plans = new Dictionary<string, AdaptiveBlueprintDetailPlan>(StringComparer.OrdinalIgnoreCase);
        foreach (var requirement in requirements)
        {
            if (!difficultyById.TryGetValue(requirement.DifficultyId, out var originalDifficulty))
                return Result<AdaptiveBlueprintResolution>.Failure(TestGenerationErrors.BlueprintUnavailable);

            adviceByTag.TryGetValue(requirement.TagId, out var mastery);
            var qualified = mastery is not null &&
                mastery.EvidenceCount >= AdaptiveBlueprintExamPolicy.MinimumEvidenceCount;
            var preferredLevel = AdaptiveBlueprintExamPolicy.ResolvePreferredLevel(
                originalDifficulty.LevelValue,
                mastery);
            var targetDifficulty = preferredLevel == originalDifficulty.LevelValue
                ? originalDifficulty
                : difficultyByLevel.GetValueOrDefault(preferredLevel);
            var preferredDifficulty = targetDifficulty ?? originalDifficulty;
            var adjusted = qualified &&
                preferredLevel != originalDifficulty.LevelValue &&
                !string.Equals(
                    preferredDifficulty.DifficultyId,
                    originalDifficulty.DifficultyId,
                    StringComparison.OrdinalIgnoreCase);

            plans[requirement.BlueprintDetailId] = new AdaptiveBlueprintDetailPlan(
                requirement.BlueprintDetailId,
                requirement.TagId,
                originalDifficulty.DifficultyId,
                preferredDifficulty.DifficultyId,
                qualified ? mastery!.OfficialPoint : null,
                qualified,
                adjusted);
        }

        var difficultyIds = plans.Values
            .SelectMany(plan => new[] { plan.OriginalDifficultyId, plan.PreferredDifficultyId })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Result<AdaptiveBlueprintResolution>.Success(new AdaptiveBlueprintResolution(plans, difficultyIds));
    }

    private async Task<(bool IsSuccessful, Result<GenerateBlueprintExamResponse> Result)> VerifySucceededAsync(
        GenerateBlueprintExamCommand command,
        string testId,
        CancellationToken cancellationToken)
    {
        var persisted = await _context.Tests
            .AsNoTracking()
            .Include(test => test.Questions)
                .ThenInclude(question => question.Question)
            .FirstOrDefaultAsync(test => test.TestId == testId, cancellationToken);
        if (persisted is null)
            return (false, default!);

        var blueprint = await _context.Blueprints
            .AsNoTracking()
            .Include(item => item.Sections)
                .ThenInclude(section => section.Details)
            .FirstOrDefaultAsync(
                item => item.BlueprintId == command.BlueprintId,
                cancellationToken);
        var orders = persisted.Questions
            .Select(question => question.QuestionOrder)
            .OrderBy(order => order)
            .ToList();
        var expectedQuantities = blueprint?.Sections
            .SelectMany(section => section.Details)
            .ToDictionary(
                detail => detail.BlueprintDetailId,
                detail => detail.Quantity,
                StringComparer.OrdinalIgnoreCase);
        var actualQuantities = persisted.Questions
            .Where(question => question.SourceBlueprintDetailId is not null)
            .GroupBy(
                question => question.SourceBlueprintDetailId!,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Count(),
                StringComparer.OrdinalIgnoreCase);
        var detailQuantitiesMatch = expectedQuantities is not null &&
            expectedQuantities.Count == actualQuantities.Count &&
            expectedQuantities.All(expected =>
                actualQuantities.TryGetValue(expected.Key, out var actual) &&
                actual == expected.Value);
        var detailsById = blueprint?.Sections
            .SelectMany(section => section.Details)
            .ToDictionary(detail => detail.BlueprintDetailId, StringComparer.OrdinalIgnoreCase);
        var auditRowsMatch = detailsById is not null &&
            persisted.Questions.All(question => IsValidAuditRow(question, detailsById));
        var uniqueQuestions = persisted.Questions
            .Select(question => question.QuestionId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == persisted.Questions.Count;
        var succeeded = blueprint?.Status == BlueprintStatuses.Active &&
            persisted.BlueprintId == command.BlueprintId &&
            persisted.GeneratedForStudentId == command.StudentId &&
            persisted.TestMode == GeneratedTestValues.BlueprintExamMode &&
            persisted.TestName == blueprint.BlueprintName &&
            persisted.DurationMinutes == blueprint.DurationMinutes &&
            persisted.TotalQuestions == persisted.Questions.Count &&
            persisted.MaxScore == blueprint.TotalScore &&
            persisted.ScoringPolicy == ScoringPolicies.BlueprintBudget &&
            persisted.Questions.Sum(question => question.MaxPointsSnapshot) == persisted.MaxScore &&
            orders.SequenceEqual(Enumerable.Range(1, persisted.TotalQuestions)) &&
            detailQuantitiesMatch &&
            uniqueQuestions &&
            auditRowsMatch;

        return succeeded
            ? (true, Result<GenerateBlueprintExamResponse>.Success(ToResponse(persisted)))
            : (false, default!);
    }

    private static bool IsValidAuditRow(
        TestQuestion question,
        IReadOnlyDictionary<string, BlueprintDetail> detailsById)
    {
        if (IsBaselineAuditRow(question))
            return true;

        if (!detailsById.TryGetValue(question.SourceBlueprintDetailId ?? string.Empty, out var detail) ||
            question.SelectionReason != GeneratedTestValues.BlueprintNormalReason ||
            !question.IsAdaptiveSelected ||
            !string.Equals(question.RecommendedForTagId, detail.TagId, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(question.RecommendedDifficultyId) ||
            question.PtagAtSelection is null ||
            question.PtagAtSelection < 0m ||
            question.PtagAtSelection > 10m ||
            question.RuleVersion != AdaptiveBlueprintExamPolicy.RuleVersion ||
            (question.Question is not null &&
             !string.Equals(question.Question.DifficultyId, question.RecommendedDifficultyId, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return HasValidSnapshot(question);
    }

    private static bool IsBaselineAuditRow(TestQuestion question)
        => HasValidSnapshot(question) &&
           !string.IsNullOrWhiteSpace(question.SourceBlueprintDetailId) &&
           question.SelectionReason == GeneratedTestValues.BlueprintNormalReason &&
           !question.IsAdaptiveSelected &&
           question.RecommendedForTagId is null &&
           question.RecommendedDifficultyId is null &&
           question.PtagAtSelection is null &&
           question.RuleVersion is null;

    private static bool HasValidSnapshot(TestQuestion question)
        => !string.IsNullOrWhiteSpace(question.QuestionVersionId) &&
           question.WeightSnapshot > 0m &&
           question.MaxPointsSnapshot >= 0m &&
           ScoringRules.IsSupported(question.ScoringRuleSnapshot) &&
           !question.IsScoreInvalidated &&
           question.InvalidatedByReportId is null;

    private static AuditValues ResolveAudit(
        PreparedBlueprintExamQuestion prepared,
        IReadOnlyDictionary<string, AdaptiveBlueprintDetailPlan> plansByDetailId)
    {
        if (!plansByDetailId.TryGetValue(prepared.Assignment.BlueprintDetailId, out var plan) ||
            !plan.HasDifficultyAdjustment ||
            !string.Equals(
                prepared.Candidate.DifficultyId,
                plan.PreferredDifficultyId,
                StringComparison.OrdinalIgnoreCase))
        {
            return new AuditValues(false, null, null, null, null);
        }

        return new AuditValues(
            true,
            plan.TagId,
            plan.PreferredDifficultyId,
            plan.OfficialPoint,
            AdaptiveBlueprintExamPolicy.RuleVersion);
    }

    private static GenerateBlueprintExamResponse ToResponse(TestEntity test)
    {
        var adaptiveQuestionCount = test.Questions.Count(question => question.IsAdaptiveSelected);
        return new GenerateBlueprintExamResponse(
            test.TestId,
            test.BlueprintId!,
            test.TestMode,
            test.TestName,
            test.DurationMinutes,
            test.TotalQuestions,
            test.MaxScore,
            test.ScoringPolicy,
            test.CreatedTime,
            adaptiveQuestionCount > 0,
            adaptiveQuestionCount,
            test.Questions.Count - adaptiveQuestionCount,
            AdaptiveBlueprintExamPolicy.RuleVersion);
    }

    private sealed record AdaptiveBlueprintResolution(
        IReadOnlyDictionary<string, AdaptiveBlueprintDetailPlan> PlansByDetailId,
        IReadOnlyCollection<string> DifficultyIds);

    private sealed record AuditValues(
        bool IsAdaptive,
        string? RecommendedForTagId,
        string? RecommendedDifficultyId,
        decimal? PtagAtSelection,
        string? RuleVersion);
}
