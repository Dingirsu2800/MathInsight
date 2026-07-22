namespace MathInsight.Modules.Testing.Persistence;

public sealed class TestReadModel
{
    public string TestId { get; set; } = string.Empty;
    public string TestName { get; set; } = string.Empty;
    public string TestMode { get; set; } = string.Empty;
    public string TestStatus { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public int TotalQuestions { get; set; }
    public decimal MaxScore { get; set; }
}

public sealed class TestQuestionReadModel
{
    public string TestId { get; set; } = string.Empty;
    public string QuestionId { get; set; } = string.Empty;
    public int QuestionOrder { get; set; }
    public string QuestionVersionId { get; set; } = string.Empty;
    public decimal MaxPointsSnapshot { get; set; }
    public string ScoringRuleSnapshot { get; set; } = string.Empty;
}

public sealed class QuestionVersionReadModel
{
    public string VersionId { get; set; } = string.Empty;
    public string QuestionId { get; set; } = string.Empty;
    public string QuestionContent { get; set; } = string.Empty;
    public string QuestionAnswer { get; set; } = string.Empty;
    public string AnswersSnapshot { get; set; } = string.Empty;
    public string? PictureUrl { get; set; }
    public short SnapshotSchemaVersion { get; set; }
}

public sealed class TestSession
{
    public string SessionId { get; set; } = string.Empty;
    public string TestId { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public string TestFormat { get; set; } = string.Empty;
    public string Status { get; set; } = "InProgress";
    public string? SubmissionType { get; set; }
    public int Duration { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int TotalQuestion { get; set; }
    public int NumCorrect { get; set; }
    public int NumIncorrect { get; set; }
    public int NumAbandoned { get; set; }
    public decimal Score { get; set; }
    public int GradeRevision { get; set; }
}

public sealed class TestAnswer
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
}

public sealed class TestAnswerOption
{
    public string TestAnswerId { get; set; } = string.Empty;
    public string AnswerId { get; set; } = string.Empty;
}

public sealed class TestAnswerPart
{
    public string TestAnswerId { get; set; } = string.Empty;
    public string PartId { get; set; } = string.Empty;
    public bool? BooleanAnswer { get; set; }
    public string? TextAnswer { get; set; }
    public decimal? NumericAnswer { get; set; }
    public bool? IsCorrect { get; set; }
    public decimal PointsEarned { get; set; }
}
