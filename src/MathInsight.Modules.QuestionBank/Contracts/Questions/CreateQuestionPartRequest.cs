namespace MathInsight.Modules.QuestionBank.Contracts.Questions;

public sealed class CreateQuestionPartRequest
{
    public int PartOrder { get; set; }
    public string? PartLabel { get; set; }
    public string PartContent { get; set; } = string.Empty;
    public string PartType { get; set; } = string.Empty;

    public bool? CorrectBoolean { get; set; }
    public string? CorrectText { get; set; }
    public decimal? CorrectNumeric { get; set; }
    public decimal? NumericTolerance { get; set; }
    public string? Explanation { get; set; }
    public decimal DefaultPoint { get; set; } = 0.00m;
}
