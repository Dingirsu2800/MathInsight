namespace MathInsight.Modules.Recommender.Persistence.Entities;

/// <summary>
/// Read-only entity for cross-module query: maps to TagDifficulty table owned by QuestionBank.
/// Used by Recommender to resolve DifficultyID from RecommendedDifficultyLevel (1..4).
/// </summary>
public class TagDifficultyReadOnly
{
    public string DifficultyId { get; set; } = string.Empty;
    public int LevelValue { get; set; }
    public string DifficultyName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
