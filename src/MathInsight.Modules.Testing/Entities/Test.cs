namespace MathInsight.Modules.Testing.Entities;

public class Test
{
    public string TestId { get; set; } = string.Empty;
    public string? BlueprintId { get; set; }
    public string TestStatus { get; set; } = "Active";
    public string TestMode { get; set; } = "BlueprintExam";
    public string? GeneratedForStudentId { get; set; }
    public string GeneratedBy { get; set; } = "System";
    public string TestName { get; set; } = string.Empty;
    public string? TestCode { get; set; }
    public int DurationMinutes { get; set; }
    public int TotalQuestions { get; set; }
    public decimal MaxScore { get; set; }
    public string ScoringPolicy { get; set; } = "NormalizedWeight";
    public DateTime CreatedTime { get; set; }

    // Navigation properties
    public Blueprint? Blueprint { get; set; }
    public ICollection<TestQuestion> Questions { get; set; } = new List<TestQuestion>();
}
