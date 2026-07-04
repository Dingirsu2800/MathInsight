namespace MathInsight.Modules.Grading_Analytics.Persistence.Entities;

/// <summary>
/// Cross-read entity — one part/statement of a COMPOSITE question (owned by QuestionBank/002).
/// Stores answer key, point value, and explanation per part.
/// Grading uses AnswerKey and PointValue to score each TestAnswerPart.
/// </summary>
public class QuestionPart
{
    public Guid QuestionPartId { get; set; }
    public Guid QuestionId { get; set; }
    public int PartOrder { get; set; }
    public string Content { get; set; } = string.Empty;

    /// <summary>The correct answer for this part (used for case-insensitive comparison)</summary>
    public string AnswerKey { get; set; } = string.Empty;

    public decimal PointValue { get; set; }
    public string? Explanation { get; set; }

    // Navigation
    public Question Question { get; set; } = null!;
}
