namespace MathInsight.Modules.QuestionBank.Entities;

public class TagDifficulty
{
    public string DifficultyId { get; set; } = default!;
    public string DifficultyName { get; set; } = default!;
    public string? Description { get; set; }
    public int LevelValue { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }

    public ICollection<Question> Questions { get; set; } = new List<Question>();
}
