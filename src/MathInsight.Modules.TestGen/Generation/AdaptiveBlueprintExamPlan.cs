namespace MathInsight.Modules.TestGen.Generation;

public sealed record AdaptiveBlueprintDetailPlan(
    string BlueprintDetailId,
    string TagId,
    string OriginalDifficultyId,
    string PreferredDifficultyId,
    decimal? OfficialPoint,
    bool HasQualifiedMastery,
    bool HasDifficultyAdjustment,
    IReadOnlyList<string> AcceptedDifficultyIds)
{
    public AdaptiveBlueprintDetailPlan(
        string blueprintDetailId,
        string tagId,
        string originalDifficultyId,
        string preferredDifficultyId,
        decimal? officialPoint,
        bool hasQualifiedMastery,
        bool hasDifficultyAdjustment)
        : this(
            blueprintDetailId,
            tagId,
            originalDifficultyId,
            preferredDifficultyId,
            officialPoint,
            hasQualifiedMastery,
            hasDifficultyAdjustment,
            BuildCompatibilityPreference(originalDifficultyId, preferredDifficultyId))
    {
    }

    private static IReadOnlyList<string> BuildCompatibilityPreference(
        string originalDifficultyId,
        string preferredDifficultyId)
        => string.Equals(originalDifficultyId, preferredDifficultyId, StringComparison.OrdinalIgnoreCase)
            ? [originalDifficultyId]
            : [preferredDifficultyId, originalDifficultyId];
}
