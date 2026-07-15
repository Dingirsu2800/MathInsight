namespace MathInsight.Modules.TestGen.Persistence.ReadModels;

public class TagDifficultyReadModel
{
    public string DifficultyId { get; set; } = string.Empty;
    public string DifficultyName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int LevelValue { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}
