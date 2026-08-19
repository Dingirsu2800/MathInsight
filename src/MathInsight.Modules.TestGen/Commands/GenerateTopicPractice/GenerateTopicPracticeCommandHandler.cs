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
using Microsoft.Extensions.Logging;

namespace MathInsight.Modules.TestGen.Commands.GenerateTopicPractice;

public sealed class GenerateTopicPracticeCommandHandler : IRequestHandler<GenerateTopicPracticeCommand, Result<GenerateTopicPracticeResponse>>
{
    private readonly TestGenDbContext _context;
    private readonly IQuestionCandidateCatalog _catalog;
    private readonly ITopicPracticeQuestionSelector _selector;
    private readonly ITopicPracticeRecommendationResolver _recommendationResolver;
    private readonly ILogger<GenerateTopicPracticeCommandHandler> _logger;

    public GenerateTopicPracticeCommandHandler(
        TestGenDbContext context,
        IQuestionCandidateCatalog catalog,
        ITopicPracticeQuestionSelector selector,
        ITopicPracticeRecommendationResolver recommendationResolver,
        ILogger<GenerateTopicPracticeCommandHandler> logger)
    {
        _context = context;
        _catalog = catalog;
        _selector = selector;
        _recommendationResolver = recommendationResolver;
        _logger = logger;
    }

    public async Task<Result<GenerateTopicPracticeResponse>> Handle(
        GenerateTopicPracticeCommand command,
        CancellationToken cancellationToken)
    {
        var prepared = await PrepareAsync(
            command,
            Guid.NewGuid().ToString("D"),
            GetUtcNowAtSqlPrecision(),
            cancellationToken);
        if (prepared.IsFailure)
            return Result<GenerateTopicPracticeResponse>.Failure(prepared.Error!);

        return await TestGenerationExecutionStrategy.ExecuteAsync(
            _context,
            () => PersistAsync(prepared.Value!, cancellationToken),
            () => VerifySucceededAsync(prepared.Value!, cancellationToken),
            cancellationToken);
    }

    private async Task<Result<PreparedTopicPracticeGeneration>> PrepareAsync(
        GenerateTopicPracticeCommand command,
        string testId,
        DateTime createdTime,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.StudentId))
            return Result<PreparedTopicPracticeGeneration>.Failure(ApplicationErrors.AuthInvalidToken);
        if (string.IsNullOrWhiteSpace(command.TagId))
            return Result<PreparedTopicPracticeGeneration>.Failure(TestGenerationErrors.RequestInvalid);

        var student = await _context.Students.AsNoTracking()
            .FirstOrDefaultAsync(item => item.StudentId == command.StudentId, cancellationToken);
        if (student is null)
            return Result<PreparedTopicPracticeGeneration>.Failure(TestGenerationErrors.TopicPracticeStudentNotFound);
        if (student.CurrentGrade is not (10 or 11 or 12))
            return Result<PreparedTopicPracticeGeneration>.Failure(TestGenerationErrors.StudentGradeRequired);

        var studentGrade = student.CurrentGrade.Value;
        var selected = await _context.TagTopics.AsNoTracking()
            .FirstOrDefaultAsync(topic => topic.TagId == command.TagId, cancellationToken);
        if (selected is null)
            return Result<PreparedTopicPracticeGeneration>.Failure(TestGenerationErrors.TopicPracticeTopicNotFound);
        if (!selected.IsActive)
            return Result<PreparedTopicPracticeGeneration>.Failure(TestGenerationErrors.TopicPracticeTopicUnavailable);
        if (selected.Grade > studentGrade)
            return Result<PreparedTopicPracticeGeneration>.Failure(TestGenerationErrors.TopicPracticeGradeNotAllowed);

        if (string.IsNullOrWhiteSpace(selected.ParentTagId))
            return Result<PreparedTopicPracticeGeneration>.Failure(TestGenerationErrors.TopicParentNotAssignable);

        var parent = await _context.TagTopics.AsNoTracking()
            .SingleOrDefaultAsync(topic => topic.TagId == selected.ParentTagId, cancellationToken);
        if (parent is null || !parent.IsActive || !string.IsNullOrWhiteSpace(parent.ParentTagId))
            return Result<PreparedTopicPracticeGeneration>.Failure(TestGenerationErrors.TopicParentNotAssignable);
        if (parent.Grade != selected.Grade)
            return Result<PreparedTopicPracticeGeneration>.Failure(TestGenerationErrors.TopicParentGradeMismatch);

        var requestedDifficultyId = string.IsNullOrWhiteSpace(command.DifficultyId)
            ? null
            : command.DifficultyId.Trim();
        var activeDifficulties = await _context.TagDifficulties.AsNoTracking()
            .Where(item => item.IsActive && item.LevelValue >= 1 && item.LevelValue <= 4)
            .ToListAsync(cancellationToken);
        var difficultyLevels = activeDifficulties.ToDictionary(
            item => item.DifficultyId,
            item => item.LevelValue,
            StringComparer.OrdinalIgnoreCase);

        var recommendation = TopicPracticeRecommendationContext.Baseline;
        string? recommendedDifficultyId = null;
        string? selectedDifficultyId = null;
        string? selectedDifficultyName = null;
        byte? selectedDifficultyLevel = null;
        var difficultySelectionMode = TopicPracticeDifficultySelectionModes.Recommended;
        TopicPracticeSelectionPlan selectionPlan;
        if (requestedDifficultyId is not null)
        {
            var requestedDifficulty = await _context.TagDifficulties.AsNoTracking()
                .SingleOrDefaultAsync(item => item.DifficultyId == requestedDifficultyId, cancellationToken);
            if (requestedDifficulty is null)
                return Result<PreparedTopicPracticeGeneration>.Failure(TestGenerationErrors.TopicPracticeDifficultyNotFound);
            if (!requestedDifficulty.IsActive || requestedDifficulty.LevelValue is < 1 or > 4)
                return Result<PreparedTopicPracticeGeneration>.Failure(TestGenerationErrors.TopicPracticeDifficultyUnavailable);

            selectedDifficultyId = requestedDifficulty.DifficultyId;
            selectedDifficultyName = requestedDifficulty.DifficultyName;
            selectedDifficultyLevel = checked((byte)requestedDifficulty.LevelValue);
            difficultySelectionMode = TopicPracticeDifficultySelectionModes.Manual;
            selectionPlan = TopicPracticeSelectionPlanFactory.CreateManual(checked((byte)requestedDifficulty.LevelValue));
        }
        else
        {
            var resolvedRecommendations = await _recommendationResolver.ResolveForTopicsAsync(
                command.StudentId,
                [selected],
                cancellationToken);
            if (resolvedRecommendations.IsFailure)
                return Result<PreparedTopicPracticeGeneration>.Failure(resolvedRecommendations.Error!);
            if (!resolvedRecommendations.Value!.TryGetValue(selected.TagId, out recommendation))
                return Result<PreparedTopicPracticeGeneration>.Failure(TestGenerationErrors.TopicPracticeRecommendationInvalid);

            if (recommendation.IsAdaptive)
            {
                var advice = recommendation.RepresentativeAdvice;
                if (advice is null)
                    return Result<PreparedTopicPracticeGeneration>.Failure(TestGenerationErrors.TopicPracticeRecommendationInvalid);

                var difficultyMatches = activeDifficulties
                    .Where(item => item.LevelValue == advice.RecommendedDifficultyLevel)
                    .Select(item => item.DifficultyId)
                    .ToList();
                if (difficultyMatches.Count != 1)
                    return Result<PreparedTopicPracticeGeneration>.Failure(TestGenerationErrors.TopicPracticeRecommendationInvalid);

                recommendedDifficultyId = difficultyMatches[0];
                try
                {
                    selectionPlan = TopicPracticeSelectionPlanFactory.CreateMastery(
                        advice.OfficialPoint,
                        recommendation.FocusTagIds,
                        AdaptiveBlueprintExamPolicy.HasNormalEvidence(advice),
                        AdaptiveBlueprintExamPolicy.HasStrongEvidence(advice));
                }
                catch (ArgumentException)
                {
                    return Result<PreparedTopicPracticeGeneration>.Failure(TestGenerationErrors.TopicPracticeRecommendationInvalid);
                }
            }
            else
            {
                selectionPlan = TopicPracticeSelectionPlanFactory.CreateBaseline();
            }
        }

        var pool = await _catalog.GetCandidatesAsync(
            new QuestionCandidateCatalogFilter(
                selected.Grade,
                [selected.TagId],
                requestedDifficultyId is null ? difficultyLevels.Keys.ToList() : [requestedDifficultyId],
                ["SingleChoice", "Composite", "ShortAnswer"]),
            cancellationToken);
        var lastSeen = await _context.TestQuestions.AsNoTracking()
            .Where(question => question.Test!.GeneratedForStudentId == command.StudentId)
            .GroupBy(question => question.QuestionId)
            .Select(group => new { QuestionId = group.Key, LastSeen = group.Max(question => question.Test!.CreatedTime) })
            .ToDictionaryAsync(item => item.QuestionId, item => (DateTime?)item.LastSeen, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var candidates = pool.Candidates
            .Where(candidate =>
                candidate.TagIds.Contains(selected.TagId) &&
                difficultyLevels.ContainsKey(candidate.DifficultyId) &&
                (requestedDifficultyId is null || string.Equals(candidate.DifficultyId, requestedDifficultyId, StringComparison.OrdinalIgnoreCase)))
            .Select(candidate => new TopicPracticeCandidate(
                candidate,
                difficultyLevels[candidate.DifficultyId],
                lastSeen.GetValueOrDefault(candidate.QuestionId)))
            .ToList();
        var selection = _selector.Select(candidates, selectionPlan, cancellationToken);
        if (!selection.IsComplete)
            return Result<PreparedTopicPracticeGeneration>.Failure(TestGenerationErrors.TopicPracticeInsufficientQuestions);

        var allocations = ScoringAllocator.Allocate(
            TopicPracticePolicy.MaxScore,
            selection.Selected.Select((item, index) => new WeightedScoreItem(
                item.Candidate.Question.QuestionId,
                item.Candidate.Question.DefaultWeight,
                index)).ToList());
        var questions = selection.Selected
            .Select((item, index) => new PreparedTopicPracticeQuestion(
                item.Candidate.Question,
                index + 1,
                allocations[item.Candidate.Question.QuestionId],
                ResolveScoringRule(item.Candidate.Question),
                item.IsWeakTagFocus))
            .ToList();

        return Result<PreparedTopicPracticeGeneration>.Success(new PreparedTopicPracticeGeneration(
            testId,
            command.StudentId,
            selected.TagId,
            selected.TagName,
            $"Luyện tập: {selected.TagName}",
            createdTime,
            recommendation,
            recommendedDifficultyId,
            difficultySelectionMode,
            selectedDifficultyId,
            selectedDifficultyName,
            selectedDifficultyLevel,
            questions));
    }

    private async Task<Result<GenerateTopicPracticeResponse>> PersistAsync(
        PreparedTopicPracticeGeneration prepared,
        CancellationToken cancellationToken)
    {
        var existing = await _context.Tests.AsNoTracking()
            .Include(item => item.Questions)
            .FirstOrDefaultAsync(item => item.TestId == prepared.TestId, cancellationToken);
        if (existing is not null)
            return BuildPersistedResult(prepared, existing);

        await using IDbContextTransaction? transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var test = new Test
        {
            TestId = prepared.TestId,
            BlueprintId = null,
            TestStatus = GeneratedTestValues.ActiveStatus,
            TestMode = "TopicPractice",
            GeneratedForStudentId = prepared.StudentId,
            GeneratedBy = GeneratedTestValues.SystemGenerator,
            TestName = prepared.TestName,
            DurationMinutes = 0,
            TotalQuestions = TopicPracticePolicy.QuestionCount,
            MaxScore = TopicPracticePolicy.MaxScore,
            ScoringPolicy = ScoringPolicies.NormalizedWeight,
            CreatedTime = prepared.CreatedTime
        };
        foreach (var preparedQuestion in prepared.Questions)
            test.Questions.Add(CreateTestQuestion(prepared, preparedQuestion));

        _context.Tests.Add(test);
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        var result = BuildPersistedResult(prepared, test);
        if (result.IsSuccess)
            LogGeneration(prepared, result.Value!);
        return result;
    }

    private async Task<(bool IsSuccessful, Result<GenerateTopicPracticeResponse> Result)> VerifySucceededAsync(
        PreparedTopicPracticeGeneration prepared,
        CancellationToken cancellationToken)
    {
        var test = await _context.Tests.AsNoTracking()
            .Include(item => item.Questions)
            .FirstOrDefaultAsync(item => item.TestId == prepared.TestId, cancellationToken);
        if (test is null)
            return (false, default!);

        var result = BuildPersistedResult(prepared, test);
        if (result.IsSuccess)
            LogGeneration(prepared, result.Value!);
        return (result.IsSuccess, result);
    }

    private static TestQuestion CreateTestQuestion(
        PreparedTopicPracticeGeneration prepared,
        PreparedTopicPracticeQuestion question)
    {
        var advice = prepared.Recommendation.RepresentativeAdvice;
        var isManual = string.Equals(
            prepared.DifficultySelectionMode,
            TopicPracticeDifficultySelectionModes.Manual,
            StringComparison.Ordinal);
        var isAdaptiveFocus = prepared.Recommendation.IsAdaptive && question.IsWeakTagFocus && advice is not null;

        return new TestQuestion
        {
            TestId = prepared.TestId,
            QuestionId = question.Question.QuestionId,
            QuestionOrder = question.QuestionOrder,
            SelectionReason = "TopicPractice",
            IsAdaptiveSelected = isAdaptiveFocus,
            RecommendedForTagId = isAdaptiveFocus ? advice!.TagId : prepared.SelectedTagId,
            RecommendedDifficultyId = isManual
                ? prepared.SelectedDifficultyId
                : isAdaptiveFocus ? prepared.RecommendedDifficultyId : null,
            PtagAtSelection = isAdaptiveFocus ? advice!.OfficialPoint : null,
            RuleVersion = isManual
                ? TopicPracticePolicy.ManualRuleVersion
                : prepared.Recommendation.IsAdaptive
                    ? TopicPracticePolicy.MasteryRuleVersion
                    : TopicPracticePolicy.RuleVersion,
            QuestionVersionId = question.Question.QuestionVersionId,
            WeightSnapshot = question.Question.DefaultWeight,
            MaxPointsSnapshot = question.MaxPoints,
            ScoringRuleSnapshot = question.ScoringRule
        };
    }

    private static Result<GenerateTopicPracticeResponse> BuildPersistedResult(
        PreparedTopicPracticeGeneration prepared,
        Test test)
    {
        if (!TopicPracticePersistenceVerifier.IsValid(test, prepared))
            return Result<GenerateTopicPracticeResponse>.Failure(TestGenerationErrors.TopicPracticeGenerationConflict);

        var adaptiveQuestionCount = test.Questions.Count(question => question.IsAdaptiveSelected);
        return Result<GenerateTopicPracticeResponse>.Success(new GenerateTopicPracticeResponse(
            test.TestId,
            prepared.SelectedTagId,
            prepared.SelectedTagName,
            test.TestName,
            test.TestMode,
            test.DurationMinutes,
            test.TotalQuestions,
            test.MaxScore,
            test.ScoringPolicy,
            test.CreatedTime,
            prepared.Recommendation.IsAdaptive,
            prepared.Recommendation.RepresentativeAdvice?.TagId,
            prepared.Recommendation.IsAdaptive ? prepared.SelectedTagName : null,
            prepared.Recommendation.RepresentativeAdvice?.RecommendedDifficultyLevel,
            adaptiveQuestionCount,
            test.Questions.Count - adaptiveQuestionCount,
            prepared.Recommendation.IsAdaptive
                ? TopicPracticePolicy.MasteryRuleVersion
                : string.Equals(prepared.DifficultySelectionMode, TopicPracticeDifficultySelectionModes.Manual, StringComparison.Ordinal)
                    ? TopicPracticePolicy.ManualRuleVersion
                    : TopicPracticePolicy.RuleVersion,
            prepared.DifficultySelectionMode,
            prepared.SelectedDifficultyId,
            prepared.SelectedDifficultyName,
            prepared.SelectedDifficultyLevel));
    }

    private void LogGeneration(
        PreparedTopicPracticeGeneration prepared,
        GenerateTopicPracticeResponse response)
    {
        var advice = prepared.Recommendation.RepresentativeAdvice;
        _logger.LogInformation(
            "Generated TopicPractice TestId {TestId} for StudentId {StudentId}, SelectedTagId {SelectedTagId}, MasteryTagId {MasteryTagId}, OfficialPoint {OfficialPoint}, EvidenceItemCount {EvidenceItemCount}, EvidenceSessionCount {EvidenceSessionCount}, RecommendedDifficultyLevel {RecommendedDifficultyLevel}, AdaptiveQuestionCount {AdaptiveQuestionCount}, FallbackQuestionCount {FallbackQuestionCount}, RuleVersion {RuleVersion}",
            response.TestId,
            prepared.StudentId,
            prepared.SelectedTagId,
            advice?.TagId,
            advice?.OfficialPoint,
            advice?.EvidenceItemCount,
            advice?.EvidenceSessionCount,
            advice?.RecommendedDifficultyLevel,
            response.AdaptiveQuestionCount,
            response.FallbackQuestionCount,
            response.RuleVersion);
    }

    private static string ResolveScoringRule(BlueprintExamCandidate question) =>
        question.SupportedScoringRules.Contains(ScoringRules.TieredTrueFalse)
            ? ScoringRules.TieredTrueFalse
            : question.SupportedScoringRules.Contains(ScoringRules.WeightedParts)
                ? ScoringRules.WeightedParts
                : ScoringRules.AllOrNothing;

    private static DateTime GetUtcNowAtSqlPrecision()
    {
        var utcNow = DateTime.UtcNow;
        return new DateTime(
            utcNow.Ticks - utcNow.Ticks % TimeSpan.TicksPerSecond,
            DateTimeKind.Utc);
    }
}
