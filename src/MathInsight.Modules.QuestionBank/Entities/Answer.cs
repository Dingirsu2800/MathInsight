namespace MathInsight.Modules.QuestionBank.Entities;

public class Answer
{
    public string AnswerId { get; set; } = default!;
    public string QuestionId { get; set; } = default!;
    public string AnswerContent { get; set; } = default!;
    public bool IsCorrect { get; set; }
    public bool IsArchived { get; set; }

    public Question Question { get; set; } = default!;
}
