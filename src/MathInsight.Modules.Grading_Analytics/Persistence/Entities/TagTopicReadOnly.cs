namespace MathInsight.Modules.Grading_Analytics.Persistence.Entities;

/// <summary>
/// Read-only entity for cross-module query: maps to TagTopic table owned by QuestionBank.
/// Used by Grading_Analytics to resolve TagName for topic breakdown rendering.
/// </summary>
public class TagTopicReadOnly
{
    public string TagId { get; set; } = string.Empty;
    public string TagName { get; set; } = string.Empty;
    public int Grade { get; set; }
}
