namespace MathInsight.Modules.Grading_Analytics.Persistence.Entities;

/// <summary>
/// Cross-read entity — owned by QuestionBank module (002).
/// Grading reads IsCorrect flag (for SINGLE_CHOICE, TRUE_FALSE, MULTIPLE_SELECT)
/// and AnswerContent (for SHORT_ANSWER case-insensitive match).
/// </summary>
public class Answer
{
    public Guid AnswerId { get; set; }
    public Guid QuestionId { get; set; }
    public string AnswerContent { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }

    // Navigation
    public Question Question { get; set; } = null!;
}
