namespace MathInsight.Modules.Grading_Analytics.Queries.GetSessionResult;

/// <summary>
/// Full session result returned by UC-55: GET /api/v1/grading/sessions/{sessionId}.
/// </summary>
public sealed record SessionResultDto
{
    public string SessionId { get; init; } = string.Empty;
    public string TestId { get; init; } = string.Empty;
    public string TestFormat { get; init; } = string.Empty;
    /// <summary>InProgress | Graded | Abandoned</summary>
    public string Status { get; init; } = string.Empty;
    public decimal Score { get; init; }
    public int NumCorrect { get; init; }
    public int NumIncorrect { get; init; }
    public int NumAbandoned { get; init; }
    public int TotalQuestion { get; init; }
    public int? DurationMinutes { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public int GradeRevision { get; init; }
    public IReadOnlyList<GradedAnswerDetailDto> Answers { get; init; } = [];
}

/// <summary>Per-answer detail for the session result view.</summary>
public sealed record GradedAnswerDetailDto
{
    public string QuestionId { get; init; } = string.Empty;
    public string QuestionVersionId { get; init; } = string.Empty;
    public int QuestionNo { get; init; }
    /// <summary>SINGLE_CHOICE | MULTIPLE_SELECT | TRUE_FALSE | SHORT_ANSWER | COMPOSITE</summary>
    public string QuestionType { get; init; } = string.Empty;
    public string QuestionContent { get; init; } = string.Empty;
    public string? PictureUrl { get; init; }
    public string SolutionContent { get; init; } = string.Empty;
    public string DifficultyId { get; init; } = string.Empty;
    public byte DifficultyLevel { get; init; }
    /// <summary>Null when session is not yet graded (InProgress).</summary>
    public bool? IsCorrect { get; init; }
    public decimal PointsEarned { get; init; }
    public decimal MachinePointsEarned { get; init; }
    public decimal EffectivePoints { get; init; }
    public decimal MaxPoints { get; init; }
    public int? TimeSpent { get; init; }
    /// <summary>For SINGLE_CHOICE / TRUE_FALSE.</summary>
    public string? SelectedOptionId { get; init; }
    /// <summary>For SHORT_ANSWER.</summary>
    public string? ShortAnswerText { get; init; }
    /// <summary>For MULTIPLE_SELECT.</summary>
    public IReadOnlyList<string> SelectedOptionIds { get; init; } = [];
    public IReadOnlyList<AnswerOptionDetailDto> AnswerOptions { get; init; } = [];
    /// <summary>For COMPOSITE.</summary>
    public IReadOnlyList<AnswerPartDetailDto> AnswerParts { get; init; } = [];
    public bool IsScoreInvalidated { get; init; }
    public string? ReportReason { get; init; }
    public DateTime? ScoreAdjustedTime { get; init; }
}

public sealed record AnswerOptionDetailDto
{
    public string AnswerId { get; init; } = string.Empty;
    public string AnswerContent { get; init; } = string.Empty;
    public bool IsCorrect { get; init; }
    public bool WasSelected { get; init; }
}

/// <summary>Per-part detail for COMPOSITE questions.</summary>
public sealed record AnswerPartDetailDto
{
    public string QuestionPartId { get; init; } = string.Empty;
    public int PartOrder { get; init; }
    public string? PartLabel { get; init; }
    public string PartContent { get; init; } = string.Empty;
    public string PartType { get; init; } = string.Empty;
    public string? StudentAnswer { get; init; }
    public string? CorrectAnswer { get; init; }
    public bool? IsCorrect { get; init; }
    public decimal PointsEarned { get; init; }
    public decimal DefaultWeight { get; init; }
}
