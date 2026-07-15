namespace MathInsight.Modules.Grading_Analytics.Queries.GetSessionResult;

/// <summary>
/// Full session result returned by UC-55: GET /api/v1/grading/sessions/{sessionId}.
/// </summary>
public sealed record SessionResultDto
{
    public Guid SessionId { get; init; }
    public Guid TestId { get; init; }
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
    public IReadOnlyList<GradedAnswerDetailDto> Answers { get; init; } = [];
}

/// <summary>Per-answer detail for the session result view.</summary>
public sealed record GradedAnswerDetailDto
{
    public Guid QuestionId { get; init; }
    public int QuestionNo { get; init; }
    /// <summary>SINGLE_CHOICE | MULTIPLE_SELECT | TRUE_FALSE | SHORT_ANSWER | COMPOSITE</summary>
    public string QuestionType { get; init; } = string.Empty;
    public string QuestionContent { get; init; } = string.Empty;
    public byte DifficultyLevel { get; init; }
    /// <summary>Null when session is not yet graded (InProgress).</summary>
    public bool? IsCorrect { get; init; }
    public decimal PointsEarned { get; init; }
    public decimal MaxPoints { get; init; }
    public int? TimeSpent { get; init; }
    /// <summary>For SINGLE_CHOICE / TRUE_FALSE.</summary>
    public Guid? SelectedOptionId { get; init; }
    /// <summary>For SHORT_ANSWER.</summary>
    public string? ShortAnswerText { get; init; }
    /// <summary>For MULTIPLE_SELECT.</summary>
    public IReadOnlyList<Guid> SelectedOptionIds { get; init; } = [];
    /// <summary>For COMPOSITE.</summary>
    public IReadOnlyList<AnswerPartDetailDto> AnswerParts { get; init; } = [];
}

/// <summary>Per-part detail for COMPOSITE questions.</summary>
public sealed record AnswerPartDetailDto
{
    public Guid QuestionPartId { get; init; }
    public string PartType { get; init; } = string.Empty;
    public string? StudentAnswer { get; init; }
    public bool? IsCorrect { get; init; }
    public decimal PointsEarned { get; init; }
}
