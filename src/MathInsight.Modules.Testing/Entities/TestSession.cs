namespace MathInsight.Modules.Testing.Entities;

public class TestSession
{
    public string SessionId { get; set; } = string.Empty;
    public string TestId { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public string TestFormat { get; set; } = "Practice"; // Practice or Exam
    public string Status { get; set; } = "InProgress"; // InProgress, Graded, Abandoned
    public string? SubmissionType { get; set; } // StudentSubmit, TimeoutSubmit, SystemSubmit
    public int Duration { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int TotalQuestion { get; set; }
    public int NumCorrect { get; set; }
    public int NumIncorrect { get; set; }
    public int NumAbandoned { get; set; }
    public decimal Score { get; set; }

    // Navigation
    public Test? Test { get; set; }
    public ICollection<TestAnswer> Answers { get; set; } = new List<TestAnswer>();
    public ICollection<TestIncident> Incidents { get; set; } = new List<TestIncident>();
}
