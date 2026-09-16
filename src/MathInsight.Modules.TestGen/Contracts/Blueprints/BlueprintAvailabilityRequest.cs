namespace MathInsight.Modules.TestGen.Contracts.Blueprints;

public sealed class BlueprintAvailabilityRequest
{
    public int Grade { get; set; }
    public List<BlueprintAvailabilitySectionRequest> Sections { get; set; } = [];
}

public sealed class BlueprintAvailabilitySectionRequest
{
    public string ClientSectionId { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public string? ScoringRule { get; set; }
    public List<BlueprintAvailabilityRowRequest> Rows { get; set; } = [];
}

public sealed class BlueprintAvailabilityRowRequest
{
    public string ClientRowId { get; set; } = string.Empty;
    public string TagId { get; set; } = string.Empty;
    public string DifficultyId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? QuestionType { get; set; }
    public string? ScoringRule { get; set; }
}
