namespace MathInsight.Modules.TestGen.Persistence.Entities;

/// <summary>
/// Blueprint entity owned by TestGen module.
/// Maps to SQL table: Blueprint.
/// </summary>
public class Blueprint
{
    public string BlueprintId { get; set; } = string.Empty;
    public string BlueprintName { get; set; } = string.Empty;
    public int Grade { get; set; }
    public int TotalQuestions { get; set; }
    public decimal TotalScore { get; set; } = 10.00m;
    public int DurationMinutes { get; set; }
    public string ExpertId { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public string? ApprovedBy { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime? ReviewTime { get; set; }

    public ICollection<BlueprintSection> Sections { get; set; } = new List<BlueprintSection>();
    public ICollection<Test> Tests { get; set; } = new List<Test>();
}
