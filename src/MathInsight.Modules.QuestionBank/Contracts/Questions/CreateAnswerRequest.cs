namespace MathInsight.Modules.QuestionBank.Contracts.Questions;

public sealed class CreateAnswerRequest
{
    public string AnswerContent { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
}
