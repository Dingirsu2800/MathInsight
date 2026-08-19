using MathInsight.Shared.Recommendations;

namespace MathInsight.Modules.TestGen.Generation;

public static class AdaptiveBlueprintExamPolicy
{
    public const string RuleVersion = "BlueprintExam-Mastery-v2";
    public const int MinimumItemCount = 5;
    public const int MinimumSessionCount = 2;
    public const int StrongItemCount = 8;
    public const int StrongSessionCount = 3;

    public static bool HasNormalEvidence(TopicMasteryAdvice? mastery)
        => mastery is not null
            && mastery.EvidenceItemCount >= MinimumItemCount
            && mastery.EvidenceSessionCount >= MinimumSessionCount;

    public static bool HasStrongEvidence(TopicMasteryAdvice? mastery)
        => mastery is not null
            && mastery.EvidenceItemCount >= StrongItemCount
            && mastery.EvidenceSessionCount >= StrongSessionCount;

    public static int ResolvePreferredLevel(
        int originalLevel,
        TopicMasteryAdvice? mastery)
    {
        var clampedOriginalLevel = Math.Clamp(originalLevel, 1, 4);
        if (!HasNormalEvidence(mastery))
            return clampedOriginalLevel;

        var offset = mastery!.OfficialPoint switch
        {
            < 2m when HasStrongEvidence(mastery) => -2,
            < 5m => -1,
            < 7.5m => 0,
            _ => 1
        };

        return Math.Clamp(clampedOriginalLevel + offset, 1, 4);
    }

    public static IReadOnlyList<int> BuildAcceptedLevels(
        int originalLevel,
        TopicMasteryAdvice? mastery)
    {
        var original = Math.Clamp(originalLevel, 1, 4);
        var preferred = ResolvePreferredLevel(original, mastery);
        if (preferred == original)
            return [original];

        if (HasStrongEvidence(mastery))
            return [preferred, original];

        if (preferred < original)
            return Enumerable.Range(preferred, original - preferred + 1).ToArray();

        return [preferred, original];
    }
}
