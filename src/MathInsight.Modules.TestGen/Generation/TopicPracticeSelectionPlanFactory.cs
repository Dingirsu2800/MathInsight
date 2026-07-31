namespace MathInsight.Modules.TestGen.Generation;

public static class TopicPracticeSelectionPlanFactory
{
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
}
