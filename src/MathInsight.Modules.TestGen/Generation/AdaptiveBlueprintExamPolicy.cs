using MathInsight.Shared.Recommendations;

namespace MathInsight.Modules.TestGen.Generation;

public static class AdaptiveBlueprintExamPolicy
{
    public const string RuleVersion = "BlueprintExam-Mastery-v1";
    public const int MinimumEvidenceCount = 3;

    public static int ResolvePreferredLevel(
        int originalLevel,
        TopicMasteryAdvice? mastery)
    {
        var clampedOriginalLevel = Math.Clamp(originalLevel, 1, 4);
        if (mastery is null || mastery.EvidenceItemCount < MinimumEvidenceCount)
            return clampedOriginalLevel;

        var offset = mastery.OfficialPoint switch
        {
            < 5m => -1,
            < 7.5m => 0,
            _ => 1
        };

        return Math.Clamp(clampedOriginalLevel + offset, 1, 4);
    }
}
