namespace MathInsight.Modules.Testing.Entities;

public class TestAnswerPart
{
    public string TestAnswerId { get; set; } = string.Empty;
    public string PartId { get; set; } = string.Empty;
    public bool? BooleanAnswer { get; set; }
    public string? TextAnswer { get; set; }
    public decimal? NumericAnswer { get; set; }
    public bool? IsCorrect { get; set; }
    public decimal PointsEarned { get; set; }

    // Navigation
    public TestAnswer? TestAnswer { get; set; }
}
