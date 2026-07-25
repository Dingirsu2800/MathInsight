namespace MathInsight.Modules.TestGen.Persistence.Entities;

/// <summary>
/// Blueprint detail entity owned by TestGen module.
/// Maps to SQL table: BlueprintDetail.
/// </summary>
public class BlueprintDetail
{
    public string BlueprintDetailId { get; set; } = string.Empty;
    public string BlueprintId { get; set; } = string.Empty;
    public string BlueprintSectionId { get; set; } = string.Empty;
    public string TagId { get; set; } = string.Empty;
    public string DifficultyId { get; set; } = string.Empty;
    public int Quantity { get; set; }

    public BlueprintSection? BlueprintSection { get; set; }
    public ICollection<TestQuestion> TestQuestions { get; set; } = new List<TestQuestion>();
}
