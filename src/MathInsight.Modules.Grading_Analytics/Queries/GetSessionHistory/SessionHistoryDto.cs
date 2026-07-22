namespace MathInsight.Modules.Grading_Analytics.Queries.GetSessionHistory;

/// <summary>
/// Summary of a single graded session, returned in the paginated history list (UC-56).
/// </summary>
public sealed record SessionHistoryDto
{
    public Guid SessionId { get; init; }
    public Guid TestId { get; init; }
    /// <summary>Practice | Exam</summary>
    public string TestFormat { get; init; } = string.Empty;
    /// <summary>Graded | Abandoned</summary>
    public string Status { get; init; } = string.Empty;
    public decimal Score { get; init; }
    public int NumCorrect { get; init; }
    public int NumIncorrect { get; init; }
    public int NumAbandoned { get; init; }
    public int TotalQuestion { get; init; }
    public int? DurationMinutes { get; init; }
    public DateTime? SubmittedAt { get; init; }
    /// <summary>StudentSubmit | TimeoutSubmit | SystemSubmit</summary>
    public string? SubmissionType { get; init; }
    /// <summary>Current grading revision. Increases when score is recalculated.</summary>
    public int GradeRevision { get; init; }
}

/// <summary>
/// Aggregate statistics across all graded sessions for a student (UC-56 /stats endpoint).
/// </summary>
public sealed record StudentHistoryStatsDto
{
    public int TotalSessions { get; init; }
    public int SessionsLast30Days { get; init; }
    /// <summary>Average score 0.00–10.00 across all graded sessions. 0 when no sessions exist.</summary>
    public decimal AverageScore { get; init; }
    /// <summary>SUM(NumCorrect) / SUM(TotalQuestion) × 100. 0 when no sessions exist.</summary>
    public decimal AccuracyPercent { get; init; }
}

/// <summary>
/// Generic paginated result wrapper used by history list endpoints.
/// </summary>
public sealed record PagedResult<T>
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public IReadOnlyList<T> Items { get; init; } = [];
}
