namespace MathInsight.Modules.TestGen.Blueprints;

public static class BlueprintStatuses
{
    public const string Draft = "Draft";
    public const string PendingReview = "PendingReview";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Active = "Active";
    public const string Deactivated = "Deactivated";

    public static string? Normalize(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;

        return status.Trim().ToUpperInvariant() switch
        {
            "DRAFT" => Draft,
            "PENDINGREVIEW" or "PENDING_REVIEW" => PendingReview,
            "APPROVED" => Approved,
            "REJECTED" => Rejected,
            "ACTIVE" => Active,
            "DEACTIVATED" => Deactivated,
            _ => string.Empty
        };
    }
}
