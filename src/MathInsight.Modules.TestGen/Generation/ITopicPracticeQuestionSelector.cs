namespace MathInsight.Modules.TestGen.Generation;

public interface ITopicPracticeQuestionSelector
{
    TopicPracticeSelection Select(IReadOnlyList<TopicPracticeCandidate> candidates, CancellationToken cancellationToken);

    TopicPracticeSelection Select(
        IReadOnlyList<TopicPracticeCandidate> candidates,
        TopicPracticeSelectionPlan plan,
        CancellationToken cancellationToken);
}
