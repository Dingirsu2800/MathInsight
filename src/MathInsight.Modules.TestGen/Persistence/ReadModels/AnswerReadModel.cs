namespace MathInsight.Modules.TestGen.Persistence.ReadModels;

public sealed class AnswerReadModel
{
    public string AnswerId { get; set; } = string.Empty;
    public string QuestionId { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public bool IsArchived { get; set; }
}
