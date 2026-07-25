namespace MathInsight.Modules.Grading_Analytics.Persistence.Entities;

/// <summary>
/// Read-only entity for cross-module query: maps to TagDifficulty table owned by QuestionBank.
/// Used by GradingOrchestrator, ScoreAdjustmentService, and queries to resolve LevelValue (1..4).
/// </summary>
public class TagDifficultyReadOnly
{
    public string DifficultyId { get; set; } = string.Empty;
    public byte LevelValue { get; set; }
}
