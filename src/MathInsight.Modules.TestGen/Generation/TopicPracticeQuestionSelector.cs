namespace MathInsight.Modules.TestGen.Generation;

public sealed class TopicPracticeQuestionSelector : ITopicPracticeQuestionSelector
{
    private readonly IGenerationRandomizer _randomizer;

    public TopicPracticeQuestionSelector(IGenerationRandomizer randomizer) => _randomizer = randomizer;

    public TopicPracticeSelection Select(IReadOnlyList<TopicPracticeCandidate> candidates, CancellationToken cancellationToken)
    {
        var remaining = candidates
            .GroupBy(candidate => candidate.Question.QuestionId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        var selected = new List<TopicPracticeCandidate>(TopicPracticePolicy.QuestionCount);
        var compositeCount = 0;

        foreach (var targetLevel in TopicPracticePolicy.TargetLevels)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var eligible = remaining
                .Where(candidate => !IsComposite(candidate) || compositeCount < TopicPracticePolicy.MaxCompositeCount)
                .ToList();
            if (eligible.Count == 0)
                return new TopicPracticeSelection(false, []);

            var ordered = eligible
                .OrderBy(candidate => Math.Abs(candidate.DifficultyLevel - targetLevel))
                .ThenBy(candidate => candidate.DifficultyLevel)
                .ThenBy(candidate => candidate.LastSeenAt is not null)
                .ThenBy(candidate => candidate.LastSeenAt)
                .ToList();
            var priority = ordered.First();
            var finalGroup = ordered
                .TakeWhile(candidate =>
                    IsComposite(candidate) == IsComposite(priority) &&
                    Math.Abs(candidate.DifficultyLevel - targetLevel) == Math.Abs(priority.DifficultyLevel - targetLevel) &&
                    candidate.DifficultyLevel == priority.DifficultyLevel &&
                    (candidate.LastSeenAt is null) == (priority.LastSeenAt is null) &&
                    candidate.LastSeenAt == priority.LastSeenAt)
                .ToList();
            _randomizer.Shuffle(finalGroup);
            var chosen = finalGroup[0];
            selected.Add(chosen);
            remaining.Remove(chosen);
            if (IsComposite(chosen))
                compositeCount++;
        }

        return selected.Count == TopicPracticePolicy.QuestionCount
            ? new TopicPracticeSelection(true, selected)
            : new TopicPracticeSelection(false, []);
    }

    private static bool IsComposite(TopicPracticeCandidate candidate) => string.Equals(candidate.Question.QuestionType, "Composite", StringComparison.OrdinalIgnoreCase);
}
