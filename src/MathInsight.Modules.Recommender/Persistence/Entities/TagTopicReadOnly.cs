namespace MathInsight.Modules.Recommender.Persistence.Entities;

/// <summary>
/// Read-only entity for cross-module query: maps to the TagTopic table owned by QuestionBank.
/// Used by Recommender to resolve tag names for weak tag DTOs.
/// This entity is NOT owned by Recommender — no writes should be made through it.
/// </summary>
public class TagTopicReadOnly
{
    public Guid TagId { get; set; }
    public string TagName { get; set; } = string.Empty;
    public int Grade { get; set; }
    public bool IsActive { get; set; }
}
