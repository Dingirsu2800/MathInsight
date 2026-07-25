namespace MathInsight.Modules.TestGen.Blueprints;

public static class BlueprintReviewActions
{
    public const string Approve = "Approve";
    public const string Reject = "Reject";

    public static string? Normalize(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
            return null;

        return action.Trim().ToUpperInvariant() switch
        {
            "APPROVE" => Approve,
            "REJECT" => Reject,
            _ => string.Empty
        };
    }
}
