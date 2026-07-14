using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Modules.TestGen.Validation;

namespace MathInsight.Modules.TestGen.Blueprints;

internal static class BlueprintAggregateFactory
{
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
