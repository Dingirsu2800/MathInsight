namespace MathInsight.Modules.Grading_Analytics.Persistence.Entities;

/// <summary>
/// Cross-read entity — one part/statement of a COMPOSITE question (owned by QuestionBank/002).
/// Stores answer key, point value, and explanation per part.
/// Grading uses AnswerKey and PointValue to score each TestAnswerPart.
/// </summary>
public class QuestionPart
{
    public string QuestionPartId { get; set; } = string.Empty;
    public string QuestionId { get; set; } = string.Empty;
    public int PartOrder { get; set; }
    public string? PartLabel { get; set; }
    public string Content { get; set; } = string.Empty;

    public bool? CorrectBoolean { get; set; }
    public string? CorrectText { get; set; }
    public decimal? CorrectNumeric { get; set; }
    public decimal? NumericTolerance { get; set; }

    public decimal DefaultWeight { get; set; }
    public bool IsArchived { get; set; }
    public string? Explanation { get; set; }

    /// <summary>TrueFalse | ShortAnswer | NumericAnswer</summary>
    public string PartType { get; set; } = string.Empty;

    // Navigation
    public Question Question { get; set; } = null!;
}
