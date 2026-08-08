namespace MathInsight.Modules.Learning_Lecture.Entities;

public sealed class TagDifficultyReadOnly
{
    public string DifficultyId { get; set; } = default!;
    public string DifficultyName { get; set; } = default!;
    public int LevelValue { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}
