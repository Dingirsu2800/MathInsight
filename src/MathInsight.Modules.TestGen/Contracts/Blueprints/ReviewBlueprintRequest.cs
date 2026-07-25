namespace MathInsight.Modules.TestGen.Contracts.Blueprints;

public sealed class ReviewBlueprintRequest
{
    public string Action { get; set; } = string.Empty;
    public string? ReviewNote { get; set; }
}
