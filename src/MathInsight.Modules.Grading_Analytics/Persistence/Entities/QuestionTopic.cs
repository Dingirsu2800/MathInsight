namespace MathInsight.Modules.Grading_Analytics.Persistence.Entities;

/// <summary>
/// Cross-read entity — owned by QuestionBank module (002).
/// Grading reads this to determine the primary topic tag for GradeCalculatedEvent.
/// MVP rule: use QuestionTopic WHERE IsPrimary = true for each question.
/// </summary>
public class QuestionTopic
{
    public string QuestionTopicId { get; set; } = string.Empty;
    public string QuestionId { get; set; } = string.Empty;
    public string TagId { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }

    // Navigation
    public Question Question { get; set; } = null!;
}
