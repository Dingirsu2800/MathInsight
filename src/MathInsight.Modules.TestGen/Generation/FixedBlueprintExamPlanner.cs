using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Shared.Scoring;

namespace MathInsight.Modules.TestGen.Generation;

public sealed record FixedBlueprintExamQuestionSelection(
    string QuestionId,
    string BlueprintDetailId,
    int QuestionOrder);

public enum FixedBlueprintExamSelectionError
{
    None,
    DuplicateQuestion,
    InvalidOrder,
    DetailQuantityMismatch,
    QuestionNotEligible,
    QuestionVersionUnavailable
}

public sealed record FixedBlueprintExamSelectionResult(
    FixedBlueprintExamSelectionError Error,
    BlueprintExamSelection Selection);

public static class FixedBlueprintExamPlanner
{
    public static FixedBlueprintExamSelectionResult Select(
        IReadOnlyList<BlueprintExamRequirement> requirements,
        IReadOnlyList<BlueprintExamCandidate> candidates,
        IReadOnlyList<FixedBlueprintExamQuestionSelection> requested)
    {
        if (requested.Select(x => x.QuestionId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != requested.Count)
            return Failure(FixedBlueprintExamSelectionError.DuplicateQuestion);

        if (!requested.Select(x => x.QuestionOrder).OrderBy(x => x)
                .SequenceEqual(Enumerable.Range(1, requested.Count)))
            return Failure(FixedBlueprintExamSelectionError.InvalidOrder);

        var requirementsById = requirements.ToDictionary(x => x.BlueprintDetailId, StringComparer.OrdinalIgnoreCase);
        var requestedCounts = requested
            .GroupBy(x => x.BlueprintDetailId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);
        if (requirements.Count != requestedCounts.Count ||
            requirements.Any(x => !requestedCounts.TryGetValue(x.BlueprintDetailId, out var count) || count != x.Quantity))
            return Failure(FixedBlueprintExamSelectionError.DetailQuantityMismatch);

        var candidatesById = candidates.ToDictionary(x => x.QuestionId, StringComparer.OrdinalIgnoreCase);
        var assignments = new List<BlueprintExamAssignment>(requested.Count);
        foreach (var item in requested.OrderBy(x => x.QuestionOrder))
        {
            if (!requirementsById.TryGetValue(item.BlueprintDetailId, out var requirement) ||
                !candidatesById.TryGetValue(item.QuestionId, out var candidate))
                return Failure(FixedBlueprintExamSelectionError.QuestionNotEligible);
            if (string.IsNullOrWhiteSpace(candidate.QuestionVersionId))
                return Failure(FixedBlueprintExamSelectionError.QuestionVersionUnavailable);
            if (!Matches(candidate, requirement))
                return Failure(FixedBlueprintExamSelectionError.QuestionNotEligible);

            assignments.Add(new BlueprintExamAssignment(
                item.QuestionId,
                item.BlueprintDetailId,
                requirement.SectionOrder,
                requirement.DetailOrder,
                item.QuestionOrder));
        }

        return new(FixedBlueprintExamSelectionError.None, new(true, assignments));
    }

    public static IReadOnlyList<PreparedBlueprintExamQuestion> PrepareQuestions(
        Blueprint blueprint,
        BlueprintExamSelection selection,
        IReadOnlyList<BlueprintExamCandidate> candidates)
    {
        var candidatesById = candidates.ToDictionary(x => x.QuestionId, StringComparer.OrdinalIgnoreCase);
        var sectionsByOrder = blueprint.Sections.ToDictionary(x => x.SectionOrder);
        var maxPointsByQuestion = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var sectionAssignments in selection.Assignments.GroupBy(x => x.SectionOrder))
        {
            var section = sectionsByOrder[sectionAssignments.Key];
            var weightedItems = sectionAssignments.Select(x => new WeightedScoreItem(
                x.QuestionId,
                candidatesById[x.QuestionId].DefaultWeight,
                x.CandidateOrder)).ToList();
            foreach (var allocation in ScoringAllocator.Allocate(section.ScoreBudget, weightedItems))
                maxPointsByQuestion.Add(allocation.Key, allocation.Value);
        }

        return selection.Assignments
            .OrderBy(x => x.CandidateOrder)
            .Select(x => new PreparedBlueprintExamQuestion(
                x,
                candidatesById[x.QuestionId],
                sectionsByOrder[x.SectionOrder].ScoringRule,
                x.CandidateOrder,
                maxPointsByQuestion[x.QuestionId]))
            .ToList();
    }

    private static bool Matches(BlueprintExamCandidate candidate, BlueprintExamRequirement requirement)
        => string.Equals(candidate.DifficultyId, requirement.DifficultyId, StringComparison.OrdinalIgnoreCase) &&
           string.Equals(candidate.QuestionType, requirement.QuestionType, StringComparison.OrdinalIgnoreCase) &&
           candidate.SupportedScoringRules.Contains(requirement.ScoringRule) &&
           (requirement.PartCountPerQuestion is null || candidate.PartCount == requirement.PartCountPerQuestion) &&
           candidate.TagIds.Contains(requirement.TagId);

    private static FixedBlueprintExamSelectionResult Failure(FixedBlueprintExamSelectionError error)
        => new(error, new(false, []));
}
