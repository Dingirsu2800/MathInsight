namespace MathInsight.Modules.Testing.Entities;

public class TestAnswer
{
    public string TestAnswerId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string QuestionId { get; set; } = string.Empty;
    public string? AnswerId { get; set; }
    public int QuestionNo { get; set; }
    public int? TimeSpent { get; set; }
    public DateTime? FirstChoiceTime { get; set; }
    public DateTime? UpdateChoiceTime { get; set; }
    public string? ShortAnswerText { get; set; }
    public bool? IsCorrect { get; set; }
    public decimal PointsEarned { get; set; }

    // Navigation
    public TestSession? Session { get; set; }
    public ICollection<TestAnswerOption> Options { get; set; } = new List<TestAnswerOption>();
    public ICollection<TestAnswerPart> Parts { get; set; } = new List<TestAnswerPart>();
}
