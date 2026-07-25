namespace MathInsight.Modules.QuestionBank.Entities;

public class QuestionPart
{
    public string PartId { get; set; } = default!;
    public string QuestionId { get; set; } = default!;
    public int PartOrder { get; set; }
    public string? PartLabel { get; set; }
    public string PartContent { get; set; } = default!;
    public string PartType { get; set; } = default!;
    public bool? CorrectBoolean { get; set; }
    public string? CorrectText { get; set; }
    public decimal? CorrectNumeric { get; set; }
    public decimal? NumericTolerance { get; set; }
    public string? Explanation { get; set; }
    public decimal DefaultWeight { get; set; } = 1.00m;
    public bool IsArchived { get; set; }
    public Question Question { get; set; } = default!;
}
