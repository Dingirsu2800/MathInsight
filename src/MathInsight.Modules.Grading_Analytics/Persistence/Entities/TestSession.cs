namespace MathInsight.Modules.Grading_Analytics.Persistence.Entities;

/// <summary>
/// Cross-read entity — owned by Testing module (003).
/// Grading reads status + writes: Status, NumCorrect, NumIncorrect, NumAbandoned, Score, SubmissionType.
/// </summary>
public class TestSession
{
    public string SessionId { get; set; } = string.Empty;
    public string TestId { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;

    /// <summary>Practice | Exam</summary>
    public string TestFormat { get; set; } = string.Empty;

    /// <summary>InProgress | Graded | Abandoned</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>StudentSubmit | TimeoutSubmit | SystemSubmit (nullable until Graded)</summary>
    public string? SubmissionType { get; set; }

    public int? Duration { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int TotalQuestion { get; set; }
    public int NumCorrect { get; set; }
    public int NumIncorrect { get; set; }
    public int NumAbandoned { get; set; }
    public decimal Score { get; set; }
    public int GradeRevision { get; set; }

    // Navigation (read-only for Grading)
    public ICollection<TestAnswer> TestAnswers { get; set; } = new List<TestAnswer>();
}
