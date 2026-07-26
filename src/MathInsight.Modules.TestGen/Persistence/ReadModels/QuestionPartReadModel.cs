namespace MathInsight.Modules.TestGen.Persistence.ReadModels;

public sealed class QuestionPartReadModel
{
    public string PartId { get; set; } = string.Empty;
    public string QuestionId { get; set; } = string.Empty;
    public int PartOrder { get; set; }
    public string PartType { get; set; } = string.Empty;
    public bool? CorrectBoolean { get; set; }
    public string? CorrectText { get; set; }
    public decimal? CorrectNumeric { get; set; }
    public decimal? NumericTolerance { get; set; }
    public decimal DefaultWeight { get; set; }
    public bool IsArchived { get; set; }
}
