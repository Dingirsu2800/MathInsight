namespace MathInsight.Modules.TestGen.Generation;

public sealed class TopicPracticeQuestionSelector : ITopicPracticeQuestionSelector
{
    private readonly IGenerationRandomizer _randomizer;

    public TopicPracticeQuestionSelector(IGenerationRandomizer randomizer) => _randomizer = randomizer;

    public TopicPracticeSelection Select(IReadOnlyList<TopicPracticeCandidate> candidates, CancellationToken cancellationToken) =>
        Select(candidates, TopicPracticeSelectionPlanFactory.CreateBaseline(), cancellationToken);

    public TopicPracticeSelection Select(
        IReadOnlyList<TopicPracticeCandidate> candidates,
        TopicPracticeSelectionPlan plan,
        CancellationToken cancellationToken)
    {
        var remaining = candidates
            .GroupBy(candidate => candidate.Question.QuestionId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        var selected = new List<SelectedTopicPracticeQuestion>(TopicPracticePolicy.QuestionCount);
        var compositeCount = 0;
        var focusCount = 0;

        foreach (var slot in plan.Slots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var eligible = remaining
                .Where(candidate => !IsComposite(candidate) || compositeCount < TopicPracticePolicy.MaxCompositeCount)
                .ToList();
            if (eligible.Count == 0)
                return new TopicPracticeSelection(false, []);

            var canApplyFocusCap = plan.FocusTagIds.Count > 0 && !plan.IsDirectFocusSelection && focusCount >= 8;
            if (canApplyFocusCap)
            {
                var nonFocusEligible = eligible.Where(candidate => !IsFocus(candidate, plan)).ToList();
                if (nonFocusEligible.Count > 0)
                    eligible = nonFocusEligible;
            }

            var ordered = eligible
                .OrderBy(candidate => GetScopePriority(candidate, slot.Scope, plan))
                .ThenBy(candidate => Math.Abs(candidate.DifficultyLevel - slot.TargetDifficultyLevel))
                .ThenBy(candidate => candidate.DifficultyLevel)
                .ThenBy(candidate => candidate.LastSeenAt is not null)
                .ThenBy(candidate => candidate.LastSeenAt)
                .ToList();
            var priority = ordered.First();
            var finalGroup = ordered
                .TakeWhile(candidate =>
                    GetScopePriority(candidate, slot.Scope, plan) == GetScopePriority(priority, slot.Scope, plan) &&
                    Math.Abs(candidate.DifficultyLevel - slot.TargetDifficultyLevel) == Math.Abs(priority.DifficultyLevel - slot.TargetDifficultyLevel) &&
                    candidate.DifficultyLevel == priority.DifficultyLevel &&
                    (candidate.LastSeenAt is null) == (priority.LastSeenAt is null) &&
                    candidate.LastSeenAt == priority.LastSeenAt)
                .ToList();
            _randomizer.Shuffle(finalGroup);
            var chosen = finalGroup[0];
            var isFocus = IsFocus(chosen, plan);
            selected.Add(new SelectedTopicPracticeQuestion(chosen, isFocus));
            remaining.Remove(chosen);
            if (IsComposite(chosen))
                compositeCount++;
            if (isFocus)
                focusCount++;
        }

        return selected.Count == TopicPracticePolicy.QuestionCount
            ? new TopicPracticeSelection(true, selected)
            : new TopicPracticeSelection(false, []);
    }

    private static bool IsComposite(TopicPracticeCandidate candidate) => string.Equals(candidate.Question.QuestionType, "Composite", StringComparison.OrdinalIgnoreCase);

    private static bool IsFocus(TopicPracticeCandidate candidate, TopicPracticeSelectionPlan plan) =>
        plan.FocusTagIds.Count > 0 && candidate.Question.TagIds.Overlaps(plan.FocusTagIds);

    private static int GetScopePriority(
        TopicPracticeCandidate candidate,
        TopicPracticeSlotScope scope,
        TopicPracticeSelectionPlan plan)
    {
        if (plan.FocusTagIds.Count == 0)
            return 0;

        var isFocus = IsFocus(candidate, plan);
        return scope == TopicPracticeSlotScope.FocusPreferred
            ? isFocus ? 0 : 1
            : isFocus ? 1 : 0;
    }
}
