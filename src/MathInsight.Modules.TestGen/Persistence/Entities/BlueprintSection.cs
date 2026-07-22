namespace MathInsight.Modules.TestGen.Persistence.Entities;

/// <summary>
/// Blueprint section entity owned by TestGen module.
/// Maps to SQL table: BlueprintSection.
/// </summary>
public class BlueprintSection
{
    public string BlueprintSectionId { get; set; } = string.Empty;
    public string BlueprintId { get; set; } = string.Empty;
    public int SectionOrder { get; set; }
    public string? SectionCode { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public string? InstructionText { get; set; }
    public int TotalQuestions { get; set; }
    public decimal ScoreBudget { get; set; }
    public string ScoringRule { get; set; } = "AllOrNothing";
    public int? PartCountPerQuestion { get; set; }

    public Blueprint? Blueprint { get; set; }
    public ICollection<BlueprintDetail> Details { get; set; } = new List<BlueprintDetail>();
}
