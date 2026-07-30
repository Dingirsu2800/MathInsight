namespace MathInsight.Modules.TestGen.Generation;

public static class TopicPracticePolicy
{
    public const int QuestionCount = 10;
    public const int MaxCompositeCount = 2;
    public const decimal MaxScore = 10.00m;
    public const string RuleVersion = "TopicPractice-v1";
    public static IReadOnlyList<int> TargetLevels { get; } = [1, 1, 1, 2, 2, 2, 2, 3, 3, 4];
}

public sealed record TopicPracticeCandidate(
    BlueprintExamCandidate Question,
    int DifficultyLevel,
    DateTime? LastSeenAt);

public sealed record TopicPracticeSelection(bool IsComplete, IReadOnlyList<TopicPracticeCandidate> Selected);
