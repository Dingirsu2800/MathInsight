using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Modules.TestGen.Commands.GenerateBlueprintExam;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Generation;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Shared.Results;
using MathInsight.Shared.Scoring;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MathInsight.Modules.TestGen.Commands.GenerateTopicPractice;

public sealed class GenerateTopicPracticeCommandHandler : IRequestHandler<GenerateTopicPracticeCommand, Result<GenerateTopicPracticeResponse>>
{
    private readonly TestGenDbContext _context;
    private readonly IQuestionCandidateCatalog _catalog;
    private readonly ITopicPracticeQuestionSelector _selector;
    public GenerateTopicPracticeCommandHandler(TestGenDbContext context, IQuestionCandidateCatalog catalog, ITopicPracticeQuestionSelector selector) { _context = context; _catalog = catalog; _selector = selector; }

    public Task<Result<GenerateTopicPracticeResponse>> Handle(GenerateTopicPracticeCommand command, CancellationToken cancellationToken)
    {
        var testId = Guid.NewGuid().ToString("D");
        var createdTime = DateTime.UtcNow;
        return TestGenerationExecutionStrategy.ExecuteAsync(
            _context,
            () => ExecuteAsync(command, testId, createdTime, cancellationToken),
            () => VerifySucceededAsync(command, testId, cancellationToken),
            cancellationToken);
    }

    private async Task<Result<GenerateTopicPracticeResponse>> ExecuteAsync(
        GenerateTopicPracticeCommand command,
        string testId,
        DateTime createdTime,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.StudentId)) return Result<GenerateTopicPracticeResponse>.Failure(ApplicationErrors.AuthInvalidToken);
        if (string.IsNullOrWhiteSpace(command.TagId)) return Result<GenerateTopicPracticeResponse>.Failure(TestGenerationErrors.RequestInvalid);

        var existing = await _context.Tests.AsNoTracking()
            .Include(item => item.Questions)
            .FirstOrDefaultAsync(item => item.TestId == testId, cancellationToken);
        if (existing is not null)
            return await BuildPersistedResultAsync(command, existing, cancellationToken);

        await using IDbContextTransaction? transaction = _context.Database.IsRelational() ? await _context.Database.BeginTransactionAsync(cancellationToken) : null;
        var grade = await _context.Students.Where(student => student.StudentId == command.StudentId).Select(student => student.CurrentGrade).FirstOrDefaultAsync(cancellationToken);
        if (grade is not (10 or 11 or 12)) return Result<GenerateTopicPracticeResponse>.Failure(TestGenerationErrors.TopicPracticeStudentNotFound);
        var studentGrade = grade.Value;
        var selected = await _context.TagTopics.AsNoTracking().FirstOrDefaultAsync(topic => topic.TagId == command.TagId, cancellationToken);
        if (selected is null) return Result<GenerateTopicPracticeResponse>.Failure(TestGenerationErrors.TopicPracticeTopicNotFound);
        if (!selected.IsActive || selected.Grade != studentGrade) return Result<GenerateTopicPracticeResponse>.Failure(TestGenerationErrors.TopicPracticeTopicUnavailable);
        var topics = await _context.TagTopics.AsNoTracking().Where(topic => topic.Grade == studentGrade && topic.IsActive).ToListAsync(cancellationToken);
        var subtree = TopicTreeResolver.ResolveActiveSubtree(selected.TagId, topics);
        var difficultyLevels = await _context.TagDifficulties.AsNoTracking().Where(item => item.IsActive && item.LevelValue >= 1 && item.LevelValue <= 4).ToDictionaryAsync(item => item.DifficultyId, item => item.LevelValue, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var pool = await _catalog.GetCandidatesAsync(new QuestionCandidateCatalogFilter(studentGrade, subtree.ToList(), difficultyLevels.Keys.ToList(), ["SingleChoice", "Composite", "ShortAnswer"]), cancellationToken);
        var lastSeen = await _context.TestQuestions.AsNoTracking().Where(question => question.Test!.GeneratedForStudentId == command.StudentId).GroupBy(question => question.QuestionId).Select(group => new { QuestionId = group.Key, LastSeen = group.Max(question => question.Test!.CreatedTime) }).ToDictionaryAsync(item => item.QuestionId, item => (DateTime?)item.LastSeen, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var candidates = pool.Candidates.Where(candidate => candidate.TagIds.Overlaps(subtree) && difficultyLevels.TryGetValue(candidate.DifficultyId, out _)).Select(candidate => new TopicPracticeCandidate(candidate, difficultyLevels[candidate.DifficultyId], lastSeen.GetValueOrDefault(candidate.QuestionId))).ToList();
        var selection = _selector.Select(candidates, cancellationToken);
        if (!selection.IsComplete) return Result<GenerateTopicPracticeResponse>.Failure(TestGenerationErrors.TopicPracticeInsufficientQuestions);
        var now = createdTime;
        var allocations = ScoringAllocator.Allocate(TopicPracticePolicy.MaxScore, selection.Selected.Select((item, index) => new WeightedScoreItem(item.Candidate.Question.QuestionId, item.Candidate.Question.DefaultWeight, index)).ToList());
        var testName = $"Luyện tập: {selected.TagName}";
        var test = new Test { TestId = testId, BlueprintId = null, TestStatus = GeneratedTestValues.ActiveStatus, TestMode = "TopicPractice", GeneratedForStudentId = command.StudentId, GeneratedBy = GeneratedTestValues.SystemGenerator, TestName = testName, DurationMinutes = 0, TotalQuestions = TopicPracticePolicy.QuestionCount, MaxScore = TopicPracticePolicy.MaxScore, ScoringPolicy = ScoringPolicies.NormalizedWeight, CreatedTime = now };
        foreach (var (item, index) in selection.Selected.Select((item, index) => (item, index))) test.Questions.Add(new TestQuestion { TestId = testId, QuestionId = item.Candidate.Question.QuestionId, QuestionOrder = index + 1, SelectionReason = "TopicPractice", IsAdaptiveSelected = false, RecommendedForTagId = selected.TagId, RecommendedDifficultyId = null, RuleVersion = TopicPracticePolicy.RuleVersion, QuestionVersionId = item.Candidate.Question.QuestionVersionId, WeightSnapshot = item.Candidate.Question.DefaultWeight, MaxPointsSnapshot = allocations[item.Candidate.Question.QuestionId], ScoringRuleSnapshot = item.Candidate.Question.SupportedScoringRules.Contains(ScoringRules.TieredTrueFalse) ? ScoringRules.TieredTrueFalse : item.Candidate.Question.SupportedScoringRules.Contains(ScoringRules.WeightedParts) ? ScoringRules.WeightedParts : ScoringRules.AllOrNothing });
        _context.Tests.Add(test); await _context.SaveChangesAsync(cancellationToken); if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return Result<GenerateTopicPracticeResponse>.Success(new GenerateTopicPracticeResponse(testId, selected.TagId, selected.TagName, testName, test.TestMode, test.DurationMinutes, test.TotalQuestions, test.MaxScore, test.ScoringPolicy, now));
    }

    private async Task<(bool IsSuccessful, Result<GenerateTopicPracticeResponse> Result)> VerifySucceededAsync(
        GenerateTopicPracticeCommand command,
        string testId,
        CancellationToken cancellationToken)
    {
        var test = await _context.Tests.AsNoTracking()
            .Include(item => item.Questions)
            .FirstOrDefaultAsync(item => item.TestId == testId, cancellationToken);
        if (test is null)
            return (false, default!);

        return (true, await BuildPersistedResultAsync(command, test, cancellationToken));
    }

    private async Task<Result<GenerateTopicPracticeResponse>> BuildPersistedResultAsync(
        GenerateTopicPracticeCommand command,
        Test test,
        CancellationToken cancellationToken)
    {
        var selectedTags = test.Questions
            .Select(question => question.RecommendedForTagId)
            .Where(tagId => !string.IsNullOrWhiteSpace(tagId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (selectedTags.Count != 1)
            return Result<GenerateTopicPracticeResponse>.Failure(TestGenerationErrors.TopicPracticeGenerationConflict);

        var selectedTag = selectedTags[0]!;

        var tagName = await _context.TagTopics.AsNoTracking()
            .Where(topic => topic.TagId == selectedTag)
            .Select(topic => topic.TagName)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(tagName))
            return Result<GenerateTopicPracticeResponse>.Failure(TestGenerationErrors.TopicPracticeGenerationConflict);

        var testName = $"Luyện tập: {tagName}";
        if (!TopicPracticePersistenceVerifier.IsValid(test, command.StudentId, selectedTag, testName))
            return Result<GenerateTopicPracticeResponse>.Failure(TestGenerationErrors.TopicPracticeGenerationConflict);

        return Result<GenerateTopicPracticeResponse>.Success(new GenerateTopicPracticeResponse(
            test.TestId, selectedTag, tagName, test.TestName, test.TestMode,
            test.DurationMinutes, test.TotalQuestions, test.MaxScore, test.ScoringPolicy, test.CreatedTime));
    }
}
