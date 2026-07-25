namespace MathInsight.Modules.QuestionBank.Contracts.Tags;

public sealed class UpdateTagDifficultyRequest
{
    public string DifficultyName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int LevelValue { get; set; }
    public int DisplayOrder { get; set; } = 1;
    public bool IsActive { get; set; } = true;
}
