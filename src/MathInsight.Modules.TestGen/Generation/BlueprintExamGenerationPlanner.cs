using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Shared.Scoring;

namespace MathInsight.Modules.TestGen.Generation;

public enum BlueprintExamStructureError
{
    None,
    Invalid,
    ScoreBudgetMismatch
}

public static class BlueprintExamGenerationPlanner
{
    public static IReadOnlyList<BlueprintExamRequirement> BuildRequirements(Blueprint blueprint)
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
                    section.ScoringRule,
                    detail.Quantity,
                    section.PartCountPerQuestion));
            }
        }

        return requirements;
    }

    public static BlueprintExamStructureError ValidateStructure(
        Blueprint blueprint,
        IReadOnlyList<BlueprintExamRequirement> requirements)
    {
        if (blueprint.TotalQuestions <= 0 ||
            blueprint.TotalScore <= 0m ||
            blueprint.DurationMinutes <= 0 ||
            blueprint.Sections.Count == 0 ||
            blueprint.Sections.Any(section =>
                section.TotalQuestions <= 0 ||
                section.ScoreBudget <= 0m ||
                !ScoringRules.IsSupported(section.ScoringRule) ||
                section.Details.Count == 0 ||
                section.Details.Sum(detail => detail.Quantity) != section.TotalQuestions) ||
            blueprint.Sections.Sum(section => section.TotalQuestions) != blueprint.TotalQuestions ||
            requirements.Any(requirement => requirement.Quantity <= 0) ||
            requirements.Sum(requirement => requirement.Quantity) != blueprint.TotalQuestions)
        {
            return BlueprintExamStructureError.Invalid;
        }

        return blueprint.Sections.Sum(section => section.ScoreBudget) == blueprint.TotalScore
            ? BlueprintExamStructureError.None
            : BlueprintExamStructureError.ScoreBudgetMismatch;
    }

    public static IReadOnlyList<PreparedBlueprintExamQuestion> PrepareQuestions(
        Blueprint blueprint,
        BlueprintExamSelection selection,
        IReadOnlyList<BlueprintExamCandidate> candidates)
    {
        var candidatesById = candidates.ToDictionary(
            candidate => candidate.QuestionId,
            StringComparer.OrdinalIgnoreCase);
        var sectionsByOrder = blueprint.Sections.ToDictionary(section => section.SectionOrder);
        var orderedAssignments = selection.Assignments
            .OrderBy(assignment => assignment.SectionOrder)
            .ThenBy(assignment => assignment.DetailOrder)
            .ThenBy(assignment => assignment.CandidateOrder)
            .ToList();
        var questionOrderById = orderedAssignments
            .Select((assignment, index) => new { assignment.QuestionId, Order = index + 1 })
            .ToDictionary(item => item.QuestionId, item => item.Order, StringComparer.OrdinalIgnoreCase);
        var maxPointsByQuestion = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var sectionAssignments in orderedAssignments.GroupBy(assignment => assignment.SectionOrder))
        {
            var section = sectionsByOrder[sectionAssignments.Key];
            var weightedItems = sectionAssignments
                .Select(assignment => new WeightedScoreItem(
                    assignment.QuestionId,
                    candidatesById[assignment.QuestionId].DefaultWeight,
                    questionOrderById[assignment.QuestionId]))
                .ToList();
            foreach (var allocation in ScoringAllocator.Allocate(section.ScoreBudget, weightedItems))
                maxPointsByQuestion.Add(allocation.Key, allocation.Value);
        }

        return orderedAssignments
            .Select(assignment => new PreparedBlueprintExamQuestion(
                assignment,
                candidatesById[assignment.QuestionId],
                sectionsByOrder[assignment.SectionOrder].ScoringRule,
                questionOrderById[assignment.QuestionId],
                maxPointsByQuestion[assignment.QuestionId]))
            .ToList();
    }
}
