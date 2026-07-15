using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Shared.Results;
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

            if (section.SectionOrder <= 0 ||
                !sectionOrders.Add(section.SectionOrder) ||
                string.IsNullOrWhiteSpace(sectionName) ||
                sectionName.Length > 100 ||
                sectionCode?.Length > 20 ||
                string.IsNullOrEmpty(questionType) ||
                section.TotalQuestions < 0 ||
                !IsPointValid(section.DefaultPointPerQuestion) ||
                !IsCompositeMetadataValid(section, questionType) ||
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
                var duplicateKey = $"{tagId}\u001F{difficultyId}";

                if (string.IsNullOrWhiteSpace(tagId) ||
                    string.IsNullOrWhiteSpace(difficultyId) ||
                    detail.Quantity < 1 ||
                    !detailKeys.Add(duplicateKey))
                {
                    return Result<ValidatedBlueprintAggregate>.Failure(BlueprintErrors.StructureInvalid);
                }

                tagIds.Add(tagId);
                difficultyIds.Add(difficultyId);
                normalizedDetails.Add(new ValidatedBlueprintDetail(tagId, difficultyId, detail.Quantity));
            }

            normalizedSections.Add(new ValidatedBlueprintSection(
                section.SectionOrder,
                sectionCode,
                sectionName,
                questionType,
                NullIfWhiteSpace(section.InstructionText),
                section.TotalQuestions,
                section.DefaultPointPerQuestion,
                section.DefaultPointPerPart,
                section.PartCountPerQuestion,
                normalizedDetails));
        }

        var tagIdValues = tagIds.ToArray();
        var difficultyIdValues = difficultyIds.ToArray();

        var activeTopics = await _context.TagTopics
            .AsNoTracking()
            .Where(topic => tagIdValues.Contains(topic.TagId) && topic.IsActive)
            .Select(topic => new { topic.TagId, topic.Grade })
            .ToListAsync(cancellationToken);

        var activeDifficulties = await _context.TagDifficulties
            .AsNoTracking()
            .Where(difficulty => difficultyIdValues.Contains(difficulty.DifficultyId) && difficulty.IsActive)
            .Select(difficulty => difficulty.DifficultyId)
            .ToListAsync(cancellationToken);

        if (activeTopics.Count != tagIds.Count ||
            activeTopics.Any(topic => topic.Grade != request.Grade) ||
            activeDifficulties.Count != difficultyIds.Count)
        {
            return Result<ValidatedBlueprintAggregate>.Failure(BlueprintErrors.TaxonomyInvalid);
        }

        return Result<ValidatedBlueprintAggregate>.Success(
            new ValidatedBlueprintAggregate(
                blueprintName,
                request.Grade,
                request.TotalQuestions,
                request.DurationMinutes,
                normalizedSections));
    }

    private static bool IsCompositeMetadataValid(BlueprintSectionRequest section, string questionType)
    {
        if (questionType == BlueprintQuestionTypes.Composite)
        {
            return section.PartCountPerQuestion > 0 &&
                section.DefaultPointPerPart is not null &&
                IsPointValid(section.DefaultPointPerPart.Value);
        }

        return section.PartCountPerQuestion is null && section.DefaultPointPerPart is null;
    }

    private static bool IsPointValid(decimal point)
        => point is >= 0m and <= 10m && decimal.Round(point, 2) == point;

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
