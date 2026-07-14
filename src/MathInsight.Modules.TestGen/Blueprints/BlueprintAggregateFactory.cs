using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Modules.TestGen.Validation;

namespace MathInsight.Modules.TestGen.Blueprints;

internal static class BlueprintAggregateFactory
{
    private const int MaxBlueprintNameLength = 100;
    private const string CopySuffix = " (Copy)";

    public static Blueprint Create(ValidatedBlueprintAggregate validated, string expertId)
    {
        var blueprint = new Blueprint
        {
            BlueprintId = Guid.NewGuid().ToString(),
            ExpertId = expertId,
            Status = BlueprintStatuses.Draft
        };

        Apply(blueprint, validated);
        return blueprint;
    }

    public static void Apply(Blueprint blueprint, ValidatedBlueprintAggregate validated)
    {
        blueprint.BlueprintName = validated.BlueprintName;
        blueprint.Grade = validated.Grade;
        blueprint.TotalQuestions = validated.TotalQuestions;
        blueprint.DurationMinutes = validated.DurationMinutes;
        blueprint.Sections = CreateSections(blueprint.BlueprintId, validated.Sections);
    }

    public static Blueprint Clone(Blueprint source, string expertId)
    {
        var blueprintId = Guid.NewGuid().ToString();
        var clone = new Blueprint
        {
            BlueprintId = blueprintId,
            BlueprintName = CreateCopyName(source.BlueprintName),
            Grade = source.Grade,
            TotalQuestions = source.TotalQuestions,
            DurationMinutes = source.DurationMinutes,
            ExpertId = expertId,
            Status = BlueprintStatuses.Draft,
            ApprovedBy = null,
            ReviewNote = null,
            ReviewTime = null
        };

        foreach (var sourceSection in source.Sections.OrderBy(section => section.SectionOrder))
        {
            var sectionId = Guid.NewGuid().ToString();
            var section = new BlueprintSection
            {
                BlueprintSectionId = sectionId,
                BlueprintId = blueprintId,
                SectionOrder = sourceSection.SectionOrder,
                SectionCode = sourceSection.SectionCode,
                SectionName = sourceSection.SectionName,
                QuestionType = sourceSection.QuestionType,
                InstructionText = sourceSection.InstructionText,
                TotalQuestions = sourceSection.TotalQuestions,
                DefaultPointPerQuestion = sourceSection.DefaultPointPerQuestion,
                DefaultPointPerPart = sourceSection.DefaultPointPerPart,
                PartCountPerQuestion = sourceSection.PartCountPerQuestion
            };

            foreach (var sourceDetail in sourceSection.Details)
            {
                section.Details.Add(new BlueprintDetail
                {
                    BlueprintDetailId = Guid.NewGuid().ToString(),
                    BlueprintId = blueprintId,
                    BlueprintSectionId = sectionId,
                    TagId = sourceDetail.TagId,
                    DifficultyId = sourceDetail.DifficultyId,
                    Quantity = sourceDetail.Quantity
                });
            }

            clone.Sections.Add(section);
        }

        return clone;
    }

    private static string CreateCopyName(string sourceName)
    {
        var maximumBaseLength = MaxBlueprintNameLength - CopySuffix.Length;
        var baseName = sourceName.Length <= maximumBaseLength
            ? sourceName
            : sourceName[..maximumBaseLength].TrimEnd();

        return baseName + CopySuffix;
    }

    private static ICollection<BlueprintSection> CreateSections(
        string blueprintId,
        IReadOnlyList<ValidatedBlueprintSection> sectionRequests)
    {
        var sections = new List<BlueprintSection>(sectionRequests.Count);

        foreach (var sectionRequest in sectionRequests)
        {
            var sectionId = Guid.NewGuid().ToString();
            var section = new BlueprintSection
            {
                BlueprintSectionId = sectionId,
                BlueprintId = blueprintId,
                SectionOrder = sectionRequest.SectionOrder,
                SectionCode = sectionRequest.SectionCode,
                SectionName = sectionRequest.SectionName,
                QuestionType = sectionRequest.QuestionType,
                InstructionText = sectionRequest.InstructionText,
                TotalQuestions = sectionRequest.TotalQuestions,
                DefaultPointPerQuestion = sectionRequest.DefaultPointPerQuestion,
                DefaultPointPerPart = sectionRequest.DefaultPointPerPart,
                PartCountPerQuestion = sectionRequest.PartCountPerQuestion
            };

            foreach (var detailRequest in sectionRequest.Details)
            {
                section.Details.Add(new BlueprintDetail
                {
                    BlueprintDetailId = Guid.NewGuid().ToString(),
                    BlueprintId = blueprintId,
                    BlueprintSectionId = sectionId,
                    TagId = detailRequest.TagId,
                    DifficultyId = detailRequest.DifficultyId,
                    Quantity = detailRequest.Quantity
                });
            }

            sections.Add(section);
        }

        return sections;
    }
}
