using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Shared.Results;
using MathInsight.Shared.Scoring;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.TestGen.Validation;

public sealed class BlueprintAggregateValidator : IBlueprintAggregateValidator
{
    private readonly TestGenDbContext _context;

    public BlueprintAggregateValidator(TestGenDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ValidatedBlueprintAggregate>> ValidateAsync(
        BlueprintRequest request,
        CancellationToken cancellationToken)
    {
        var blueprintName = request.BlueprintName?.Trim();
        if (string.IsNullOrWhiteSpace(blueprintName) ||
            blueprintName.Length > 100 ||
            request.Grade is not (10 or 11 or 12) ||
            request.TotalQuestions < 0 ||
            !IsScoreValid(request.TotalScore) ||
            request.DurationMinutes < 0)
        {
            return Result<ValidatedBlueprintAggregate>.Failure(BlueprintErrors.RequestInvalid);
        }

        if (request.Sections is null || request.Sections.Count == 0)
            return Result<ValidatedBlueprintAggregate>.Failure(BlueprintErrors.StructureInvalid);

        var sectionOrders = new HashSet<int>();
        var normalizedSections = new List<ValidatedBlueprintSection>(request.Sections.Count);
        var tagIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var difficultyIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var section in request.Sections)
        {
            var sectionName = section.SectionName?.Trim();
            var sectionCode = NullIfWhiteSpace(section.SectionCode);
            var questionType = BlueprintQuestionTypes.Normalize(section.QuestionType);
            var scoringRule = BlueprintPolicies.NormalizeScoringRule(section.ScoringRule);

            if (section.SectionOrder <= 0 ||
                !sectionOrders.Add(section.SectionOrder) ||
                string.IsNullOrWhiteSpace(sectionName) ||
                sectionName.Length > 100 ||
                sectionCode?.Length > 20 ||
                string.IsNullOrEmpty(questionType) ||
                section.TotalQuestions < 0 ||
                !IsScoreValid(section.ScoreBudget) ||
                (questionType != BlueprintQuestionTypes.Composite && section.PartCountPerQuestion is not null) ||
                !IsSectionPolicyValid(questionType, scoringRule) ||
                section.Details is null ||
                section.Details.Count == 0)
            {
                return Result<ValidatedBlueprintAggregate>.Failure(BlueprintErrors.StructureInvalid);
            }

            var detailKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var normalizedDetails = new List<ValidatedBlueprintDetail>(section.Details.Count);

            foreach (var detail in section.Details)
            {
                var tagId = detail.TagId?.Trim();
                var difficultyId = detail.DifficultyId?.Trim();
                var detailQuestionType = BlueprintQuestionTypes.Normalize(detail.QuestionType);
                var detailScoringRule = BlueprintPolicies.NormalizeScoringRule(detail.ScoringRule);
                var duplicateKey = $"{tagId}\u001F{difficultyId}\u001F{detailQuestionType}\u001F{detailScoringRule}";

                if (string.IsNullOrWhiteSpace(tagId) ||
                    string.IsNullOrWhiteSpace(difficultyId) ||
                    detail.Quantity < 1 ||
                    !detailKeys.Add(duplicateKey) ||
                    !BlueprintPolicies.TryResolveEffectivePolicy(
                        questionType,
                        scoringRule,
                        detailQuestionType,
                        detailScoringRule,
                        out var effectiveQuestionType,
                        out var effectiveScoringRule))
                {
                    return Result<ValidatedBlueprintAggregate>.Failure(BlueprintErrors.StructureInvalid);
                }

                tagIds.Add(tagId);
                difficultyIds.Add(difficultyId);
                normalizedDetails.Add(new ValidatedBlueprintDetail(
                    tagId,
                    difficultyId,
                    detail.Quantity,
                    questionType == BlueprintQuestionTypes.Mixed ? effectiveQuestionType : null,
                    questionType == BlueprintQuestionTypes.Mixed ? effectiveScoringRule : null));
            }

            normalizedSections.Add(new ValidatedBlueprintSection(
                section.SectionOrder,
                sectionCode,
                sectionName,
                questionType,
                NullIfWhiteSpace(section.InstructionText),
                section.TotalQuestions,
                section.ScoreBudget,
                questionType == BlueprintQuestionTypes.Mixed ? null : scoringRule,
                null,
                normalizedDetails));
        }

        var tagIdValues = tagIds.ToArray();
        var difficultyIdValues = difficultyIds.ToArray();

        var activeTopics = await _context.TagTopics
            .AsNoTracking()
            .Where(topic => tagIdValues.Contains(topic.TagId) && topic.IsActive)
            .Select(topic => new { topic.TagId, topic.Grade, topic.ParentTagId })
            .ToListAsync(cancellationToken);

        var parentTagIds = activeTopics
            .Where(topic => !string.IsNullOrWhiteSpace(topic.ParentTagId))
            .Select(topic => topic.ParentTagId!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var activeParents = await _context.TagTopics
            .AsNoTracking()
            .Where(topic => parentTagIds.Contains(topic.TagId) && topic.IsActive)
            .Select(topic => new { topic.TagId, topic.Grade, topic.ParentTagId })
            .ToListAsync(cancellationToken);

        var parentsById = activeParents.ToDictionary(topic => topic.TagId, StringComparer.OrdinalIgnoreCase);

        var activeDifficulties = await _context.TagDifficulties
            .AsNoTracking()
            .Where(difficulty => difficultyIdValues.Contains(difficulty.DifficultyId) && difficulty.IsActive)
            .Select(difficulty => difficulty.DifficultyId)
            .ToListAsync(cancellationToken);

        if (activeTopics.Count != tagIds.Count ||
            activeTopics.Any(topic => topic.Grade != request.Grade) ||
            activeTopics.Any(topic => string.IsNullOrWhiteSpace(topic.ParentTagId) ||
                !parentsById.TryGetValue(topic.ParentTagId!, out var parent) ||
                !string.IsNullOrWhiteSpace(parent.ParentTagId) ||
                parent.Grade != topic.Grade) ||
            activeDifficulties.Count != difficultyIds.Count)
        {
            return Result<ValidatedBlueprintAggregate>.Failure(BlueprintErrors.TaxonomyInvalid);
        }

        if (normalizedSections.Sum(section => section.ScoreBudget) != request.TotalScore)
            return Result<ValidatedBlueprintAggregate>.Failure(BlueprintErrors.StructureInvalid);

        return Result<ValidatedBlueprintAggregate>.Success(
            new ValidatedBlueprintAggregate(
                blueprintName,
                request.Grade,
                request.TotalQuestions,
                request.TotalScore,
                request.DurationMinutes,
                normalizedSections));
    }

    private static bool IsSectionPolicyValid(string? questionType, string? scoringRule)
        => questionType == BlueprintQuestionTypes.Mixed
            ? scoringRule is null
            : BlueprintQuestionTypes.IsActualQuestionType(questionType) &&
              BlueprintPolicies.IsValidPair(questionType, scoringRule);

    private static bool IsScoreValid(decimal score)
        => score is > 0m and <= 100m && decimal.Round(score, 2) == score;

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
