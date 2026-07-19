namespace MathInsight.Shared.Events;

/// <summary>
/// Published by the Grading module (004) after a TestSession is graded.
/// Consumers:
///   - Recommender module (005): updates TagsMastery / StudentTopicSessionResult per tag.
///   - Notification module (008): sends "test graded" push notification to the student.
///
/// All per-tag data required by the Recommender is included in PerTagResults.
/// Consumers must be idempotent — duplicate events for the same SessionId must be safe to ignore.
/// </summary>
public sealed record GradeCalculatedEvent : MediatR.INotification
{
    public Guid SessionId { get; init; }
    public Guid StudentId { get; init; }
    public Guid TestId { get; init; }
    public string TestFormat { get; init; } = string.Empty;

    /// <summary>
    /// Normalized score 0.00–10.00. Formula: SUM(points_earned) / SUM(max_points) × 10.0 (BR-20).
    /// </summary>
    public decimal Score { get; init; }

    public int NumCorrect { get; init; }
    public int NumIncorrect { get; init; }

    /// <summary>
    /// Questions where the student did not submit any answer (abandoned per BR-16b:
    ///   - SINGLE_CHOICE/TRUE_FALSE: answer_id is null
    ///   - MULTIPLE_SELECT: no options selected (no associated TestAnswerOption records)
    ///   - SHORT_ANSWER: short_answer_text is null or whitespace
    ///   - COMPOSITE: all child parts are unanswered/abandoned (all TestAnswerPart.student_answer are null/empty)
    /// </summary>
    public int NumAbandoned { get; init; }

    /// <summary>
    /// Per-topic grading summary. Required by Recommender (005) to update TagsMastery.
    /// One entry per distinct TagId covered in the session.
    /// <b>TagId here is a TagTopic (topic tag) ID, not a TagDifficulty DifficultyID.</b>
    /// </summary>
    public IReadOnlyList<TopicGradeResult> PerTagResults { get; init; } = [];

    /// <summary>
    /// Detailed list of graded answers for Elo calculation.
    /// </summary>
    public IReadOnlyList<GradedAnswerDto> Answers { get; init; } = [];

    public DateTime GradedAt { get; init; }
}

/// <summary>
/// Detailed answer info for Elo calculation.
/// </summary>
public sealed record GradedAnswerDto
{
    public Guid QuestionId { get; init; }
    public Guid TagId { get; init; }
    public bool IsCorrect { get; init; }
    public decimal PointsEarned { get; init; }
    public decimal MaxPoints { get; init; }
    public int TimeSpent { get; init; }
    public byte DifficultyLevel { get; init; }
    public int QuestionNo { get; init; }
    /// <summary>
    /// True if the student did not submit any answer for this question (abandoned per BR-16b).
    /// </summary>
    public bool IsAbandoned { get; init; }
}

/// <summary>
/// Per-topic grading result snapshot for a single (SessionId, TagId) pair.
/// Used by Recommender to update TagsMastery and insert StudentTopicSessionResult.
/// </summary>
public sealed record TopicGradeResult
{
    public Guid TagId { get; init; }

    /// <summary>
    /// Normalized topic score 0.00–10.00: correct_count / total_count × 10.0.
    /// </summary>
    public decimal TopicScore { get; init; }

    public int CorrectCount { get; init; }
    public int TotalCount { get; init; }
}