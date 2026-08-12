namespace MathInsight.Modules.TestGen.Generation;

public static class TopicPracticePolicy
{
    public const int QuestionCount = 10;
    public const int MaxCompositeCount = 2;
    public const decimal MaxScore = 10.00m;
    public const string RuleVersion = "TopicPractice-v1";
    public const string WeakTagRuleVersion = "TopicPractice-WeakTag-v1";
    public const string ManualRuleVersion = "TopicPractice-Manual-v1";
}

public static class TopicPracticeDifficultySelectionModes
{
    public const string Recommended = "Recommended";
    public const string Manual = "Manual";
}

public enum TopicPracticeSlotScope
{
    FocusPreferred,
    BreadthPreferred
}

public sealed record TopicPracticeSlot(int TargetDifficultyLevel, TopicPracticeSlotScope Scope);

public sealed record TopicPracticeSelectionPlan(
    IReadOnlyList<TopicPracticeSlot> Slots,
    IReadOnlySet<string> FocusTagIds,
    bool IsDirectFocusSelection,
    string RuleVersion);

public sealed record TopicPracticeCandidate(
    BlueprintExamCandidate Question,
    int DifficultyLevel,
    DateTime? LastSeenAt);

public sealed record SelectedTopicPracticeQuestion(TopicPracticeCandidate Candidate, bool IsWeakTagFocus);

public sealed record TopicPracticeSelection(bool IsComplete, IReadOnlyList<SelectedTopicPracticeQuestion> Selected);
