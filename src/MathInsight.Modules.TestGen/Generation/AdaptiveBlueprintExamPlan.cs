namespace MathInsight.Modules.TestGen.Generation;

public sealed record AdaptiveBlueprintDetailPlan(
    string BlueprintDetailId,
    string TagId,
    string OriginalDifficultyId,
    string PreferredDifficultyId,
    decimal? OfficialPoint,
    bool HasQualifiedMastery,
    bool HasDifficultyAdjustment);
