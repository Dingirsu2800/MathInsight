namespace MathInsight.Modules.TestGen.Contracts.Blueprints;

public sealed class BlueprintRequest
{
    public string BlueprintName { get; set; } = string.Empty;
    public int Grade { get; set; }
    public int TotalQuestions { get; set; }
    public int DurationMinutes { get; set; }
    public List<BlueprintSectionRequest> Sections { get; set; } = [];
}

public sealed class BlueprintSectionRequest
{
    public int SectionOrder { get; set; }
    public string? SectionCode { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public string? InstructionText { get; set; }
    public int TotalQuestions { get; set; }
    public decimal DefaultPointPerQuestion { get; set; }
    public decimal? DefaultPointPerPart { get; set; }
    public int? PartCountPerQuestion { get; set; }
    public List<BlueprintDetailRequest> Details { get; set; } = [];
}

public sealed class BlueprintDetailRequest
{
    public string TagId { get; set; } = string.Empty;
    public string DifficultyId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
