namespace MathInsight.Modules.Grading_Analytics.Persistence.Entities;

/// <summary>
/// Cross-read entity — one part/statement of a COMPOSITE question (owned by QuestionBank/002).
/// Stores answer key, weight ratio, and explanation per part.
/// Grading uses AnswerKey and DefaultWeight to distribute score within Composite.
/// Part weights only distribute points internally; they do not increase the total question weight.
/// </summary>
public class QuestionPart
{
    public Guid QuestionPartId { get; set; }
    public Guid QuestionId { get; set; }
    public int PartOrder { get; set; }
    public string? PartLabel { get; set; }
    public string Content { get; set; } = string.Empty;

    public bool? CorrectBoolean { get; set; }
    public string? CorrectText { get; set; }
    public decimal? CorrectNumeric { get; set; }
    public decimal? NumericTolerance { get; set; }

    /// <summary>Weight ratio for distributing points among parts within Composite.</summary>
    public decimal DefaultWeight { get; set; }
    public string? Explanation { get; set; }

    /// <summary>TrueFalse | ShortAnswer | NumericAnswer</summary>
    public string PartType { get; set; } = string.Empty;

    // Navigation
    public Question Question { get; set; } = null!;
}
