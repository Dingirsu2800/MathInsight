namespace MathInsight.Modules.Grading_Analytics.Persistence.Entities;

/// <summary>
/// Cross-read entity — owned by QuestionBank module (002).
/// Grading reads DefaultWeight and question type for scoring.
/// Actual points per question come from TestQuestion.MaxPointsSnapshot.
/// </summary>
public class Question
{
    public string QuestionId { get; set; } = string.Empty;

    /// <summary>SINGLE_CHOICE | MULTIPLE_SELECT | TRUE_FALSE | SHORT_ANSWER | COMPOSITE</summary>
    public string QuestionType { get; set; } = string.Empty;

    /// <summary>Weight of the question (default 1.00). Not the final score — use TestQuestion.MaxPointsSnapshot.</summary>
    public decimal DefaultWeight { get; set; }

    /// <summary>Difficulty level value (1..4) from TagDifficulty, for GradeCalculatedEvent.</summary>
    public string DifficultyId { get; set; } = string.Empty;

    /// <summary>
    /// Question text content — cross-read from QuestionBank.
    /// Used by ChatbotService (UC-51) to send question context to the AI API.
    /// </summary>
    public string QuestionContent { get; set; } = string.Empty;

    public string? SolutionContent { get; set; }
    public string? PictureUrl { get; set; }

    // Navigation
    public ICollection<Answer> Answers { get; set; } = new List<Answer>();
    public ICollection<QuestionPart> Parts { get; set; } = new List<QuestionPart>();
    public ICollection<QuestionTopic> QuestionTopics { get; set; } = new List<QuestionTopic>();
}
