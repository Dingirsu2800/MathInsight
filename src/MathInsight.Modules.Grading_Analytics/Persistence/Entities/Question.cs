namespace MathInsight.Modules.Grading_Analytics.Persistence.Entities;

/// <summary>
/// Cross-read entity — owned by QuestionBank module (002).
/// Grading reads DefaultPoint and question type for scoring.
/// </summary>
public class Question
{
    public Guid QuestionId { get; set; }

    /// <summary>SINGLE_CHOICE | MULTIPLE_SELECT | TRUE_FALSE | SHORT_ANSWER | COMPOSITE</summary>
    public string QuestionType { get; set; } = string.Empty;

    public decimal DefaultPoint { get; set; }

    // Navigation
    public ICollection<Answer> Answers { get; set; } = new List<Answer>();
    public ICollection<QuestionPart> Parts { get; set; } = new List<QuestionPart>();
    public ICollection<QuestionTopic> QuestionTopics { get; set; } = new List<QuestionTopic>();
}
