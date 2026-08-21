namespace MathInsight.Modules.TestGen.Generation;

public static class TopicPracticeSelectionPlanFactory
{
    public static TopicPracticeSelectionPlan CreateManual(byte difficultyLevel)
    {
        if (difficultyLevel is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(difficultyLevel));

        return new TopicPracticeSelectionPlan(
            Enumerable.Repeat(
                    new TopicPracticeSlot(difficultyLevel, TopicPracticeSlotScope.BreadthPreferred),
                    TopicPracticePolicy.QuestionCount)
                .ToList(),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            IsDirectFocusSelection: false,
            TopicPracticePolicy.ManualRuleVersion);
    }

    public static TopicPracticeSelectionPlan CreateBaseline() => new(
        [
            new(1, TopicPracticeSlotScope.BreadthPreferred),
            new(1, TopicPracticeSlotScope.BreadthPreferred),
            new(1, TopicPracticeSlotScope.BreadthPreferred),
            new(2, TopicPracticeSlotScope.BreadthPreferred),
            new(2, TopicPracticeSlotScope.BreadthPreferred),
            new(2, TopicPracticeSlotScope.BreadthPreferred),
            new(2, TopicPracticeSlotScope.BreadthPreferred),
            new(3, TopicPracticeSlotScope.BreadthPreferred),
            new(3, TopicPracticeSlotScope.BreadthPreferred),
            new(4, TopicPracticeSlotScope.BreadthPreferred)
        ],
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        IsDirectFocusSelection: false,
        TopicPracticePolicy.RuleVersion);

    public static TopicPracticeSelectionPlan CreateAdaptive(
        byte recommendedDifficultyLevel,
        IReadOnlySet<string> focusTagIds,
        bool isDirectFocusSelection)
    {
        if (focusTagIds.Count == 0)
            throw new ArgumentException("Adaptive TopicPractice requires at least one focus tag.", nameof(focusTagIds));

        var slots = recommendedDifficultyLevel switch
        {
            1 => new[]
            {
                new TopicPracticeSlot(1, TopicPracticeSlotScope.FocusPreferred),
                new TopicPracticeSlot(1, TopicPracticeSlotScope.FocusPreferred),
                new TopicPracticeSlot(1, TopicPracticeSlotScope.FocusPreferred),
                new TopicPracticeSlot(1, TopicPracticeSlotScope.FocusPreferred),
                new TopicPracticeSlot(1, TopicPracticeSlotScope.FocusPreferred),
                new TopicPracticeSlot(2, TopicPracticeSlotScope.FocusPreferred),
                new TopicPracticeSlot(1, TopicPracticeSlotScope.BreadthPreferred),
                new TopicPracticeSlot(1, TopicPracticeSlotScope.BreadthPreferred),
                new TopicPracticeSlot(1, TopicPracticeSlotScope.BreadthPreferred),
                new TopicPracticeSlot(2, TopicPracticeSlotScope.BreadthPreferred)
            },
            2 => new[]
            {
                new TopicPracticeSlot(1, TopicPracticeSlotScope.FocusPreferred),
                new TopicPracticeSlot(2, TopicPracticeSlotScope.FocusPreferred),
                new TopicPracticeSlot(2, TopicPracticeSlotScope.FocusPreferred),
                new TopicPracticeSlot(2, TopicPracticeSlotScope.FocusPreferred),
                new TopicPracticeSlot(2, TopicPracticeSlotScope.FocusPreferred),
                new TopicPracticeSlot(3, TopicPracticeSlotScope.FocusPreferred),
                new TopicPracticeSlot(1, TopicPracticeSlotScope.BreadthPreferred),
                new TopicPracticeSlot(2, TopicPracticeSlotScope.BreadthPreferred),
                new TopicPracticeSlot(2, TopicPracticeSlotScope.BreadthPreferred),
                new TopicPracticeSlot(2, TopicPracticeSlotScope.BreadthPreferred)
            },
            _ => throw new ArgumentOutOfRangeException(
                nameof(recommendedDifficultyLevel),
                recommendedDifficultyLevel,
                "Only recommendation levels 1 and 2 are supported for TopicPractice.")
        };

        return new TopicPracticeSelectionPlan(
            slots,
            new HashSet<string>(focusTagIds, StringComparer.OrdinalIgnoreCase),
            isDirectFocusSelection,
            TopicPracticePolicy.WeakTagRuleVersion);
    }

    public static TopicPracticeSelectionPlan CreateMastery(
        decimal officialPoint,
        IReadOnlySet<string> focusTagIds)
        => CreateMastery(officialPoint, focusTagIds, hasNormalEvidence: true, hasStrongEvidence: true);

    public static TopicPracticeSelectionPlan CreateMastery(
        decimal officialPoint,
        IReadOnlySet<string> focusTagIds,
        bool hasNormalEvidence,
        bool hasStrongEvidence)
    {
        if (focusTagIds.Count == 0)
            throw new ArgumentException("Mastery TopicPractice requires one selected topic.", nameof(focusTagIds));

        int[] profile = !hasNormalEvidence
            ? [3, 4, 2, 1]
            : Math.Clamp(officialPoint, 0m, 10m) switch
            {
                < 2m when hasStrongEvidence => [9, 1, 0, 0],
                < 2m => [8, 2, 0, 0],
                < 3m => [8, 2, 0, 0],
                < 4m => [3, 6, 1, 0],
                < 5m => [2, 6, 2, 0],
                < 6m => [0, 3, 6, 1],
                < 7.5m => [0, 2, 6, 2],
                < 9m => [0, 0, 2, 8],
                _ when hasStrongEvidence => [0, 0, 1, 9],
                _ => [0, 0, 2, 8]
            };

        var slots = profile
            .SelectMany((count, index) => Enumerable.Repeat(
                new TopicPracticeSlot(index + 1, TopicPracticeSlotScope.FocusPreferred), count))
            .ToList();

        return new TopicPracticeSelectionPlan(
            slots,
            new HashSet<string>(focusTagIds, StringComparer.OrdinalIgnoreCase),
            IsDirectFocusSelection: true,
            TopicPracticePolicy.MasteryRuleVersion);
    }
}
