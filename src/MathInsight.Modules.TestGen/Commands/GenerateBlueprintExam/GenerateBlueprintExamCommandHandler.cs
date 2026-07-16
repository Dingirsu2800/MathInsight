using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Commands.Common;
using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Generation;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Shared.Results;
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

    public GenerateBlueprintExamCommandHandler(
        TestGenDbContext context,
        IBlueprintExamCandidateProvider candidateProvider,
        IBlueprintExamQuestionSelector selector)
    {
        _context = context;
        _candidateProvider = candidateProvider;
        _selector = selector;
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

        var requirements = BuildRequirements(blueprint);
        if (!HasValidStructure(blueprint, requirements))
            return Result<GenerateBlueprintExamResponse>.Failure(TestGenerationErrors.BlueprintUnavailable);

        var candidates = await _candidateProvider.GetCandidatesAsync(blueprint, cancellationToken);
        var selection = _selector.Select(requirements, candidates, cancellationToken);
        if (!selection.IsComplete || selection.Assignments.Count != blueprint.TotalQuestions)
            return Result<GenerateBlueprintExamResponse>.Failure(TestGenerationErrors.InsufficientQuestions);

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
            CreatedTime = createdTime
        };

        for (var index = 0; index < selection.Assignments.Count; index++)
        {
            var assignment = selection.Assignments[index];
            test.Questions.Add(new TestQuestion
            {
                TestId = test.TestId,
                QuestionId = assignment.QuestionId,
                QuestionOrder = index + 1,
                SourceBlueprintDetailId = assignment.BlueprintDetailId,
                SelectionReason = GeneratedTestValues.BlueprintNormalReason,
                IsAdaptiveSelected = false,
                RecommendedForTagId = null,
                RecommendedDifficultyId = null,
                PtagAtSelection = null,
                RuleVersion = null
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

    private async Task<(bool IsSuccessful, Result<GenerateBlueprintExamResponse> Result)> VerifySucceededAsync(
        GenerateBlueprintExamCommand command,
        string testId,
        CancellationToken cancellationToken)
    {
        var persisted = await _context.Tests
            .AsNoTracking()
            .Include(test => test.Questions)
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
        var succeeded = blueprint?.Status == BlueprintStatuses.Active &&
            persisted.BlueprintId == command.BlueprintId &&
            persisted.GeneratedForStudentId == command.StudentId &&
            persisted.TestMode == GeneratedTestValues.BlueprintExamMode &&
            persisted.TestName == blueprint.BlueprintName &&
            persisted.DurationMinutes == blueprint.DurationMinutes &&
            persisted.TotalQuestions == persisted.Questions.Count &&
            orders.SequenceEqual(Enumerable.Range(1, persisted.TotalQuestions)) &&
            detailQuantitiesMatch &&
            persisted.Questions.All(IsBaselineAuditRow);

        return succeeded
            ? (true, Result<GenerateBlueprintExamResponse>.Success(ToResponse(persisted)))
            : (false, default!);
    }

    private static IReadOnlyList<BlueprintExamRequirement> BuildRequirements(Blueprint blueprint)
    {
        var requirements = new List<BlueprintExamRequirement>();
        var detailOrder = 0;
        foreach (var section in blueprint.Sections.OrderBy(section => section.SectionOrder))
        {
            foreach (var detail in section.Details
                         .OrderBy(detail => detail.TagId, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(detail => detail.DifficultyId, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(detail => detail.BlueprintDetailId, StringComparer.OrdinalIgnoreCase))
            {
                requirements.Add(new BlueprintExamRequirement(
                    detail.BlueprintDetailId,
                    section.SectionOrder,
                    detailOrder++,
                    detail.TagId,
                    detail.DifficultyId,
                    section.QuestionType,
                    detail.Quantity));
            }
        }

        return requirements;
    }

    private static bool HasValidStructure(
        Blueprint blueprint,
        IReadOnlyList<BlueprintExamRequirement> requirements)
        => blueprint.TotalQuestions > 0 &&
           blueprint.DurationMinutes > 0 &&
           blueprint.Sections.Count > 0 &&
           blueprint.Sections.All(section =>
               section.TotalQuestions > 0 &&
               section.Details.Count > 0 &&
               section.Details.Sum(detail => detail.Quantity) == section.TotalQuestions) &&
           blueprint.Sections.Sum(section => section.TotalQuestions) == blueprint.TotalQuestions &&
           requirements.All(requirement => requirement.Quantity > 0) &&
           requirements.Sum(requirement => requirement.Quantity) == blueprint.TotalQuestions;

    private static bool IsBaselineAuditRow(TestQuestion question)
        => !string.IsNullOrWhiteSpace(question.SourceBlueprintDetailId) &&
           question.SelectionReason == GeneratedTestValues.BlueprintNormalReason &&
           !question.IsAdaptiveSelected &&
           question.RecommendedForTagId is null &&
           question.RecommendedDifficultyId is null &&
           question.PtagAtSelection is null &&
           question.RuleVersion is null;

    private static GenerateBlueprintExamResponse ToResponse(TestEntity test)
        => new(
            test.TestId,
            test.BlueprintId!,
            test.TestMode,
            test.TestName,
            test.DurationMinutes,
            test.TotalQuestions,
            test.CreatedTime);
}
