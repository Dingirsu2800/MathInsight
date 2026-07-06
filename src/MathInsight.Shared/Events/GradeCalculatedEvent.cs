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

    /// <summary>
    /// Normalized score 0.00–10.00. Formula: SUM(points_earned) / total_questions × 10.0 (BR-20).
    /// </summary>
    public decimal Score { get; init; }

    public int NumCorrect { get; init; }
    public int NumIncorrect { get; init; }

    /// <summary>
    /// Questions where the student did not submit any answer (answer_id = null).
    /// </summary>
    public int NumAbandoned { get; init; }

    /// <summary>
    /// Per-topic grading summary. Required by Recommender (005) to update TagsMastery.
    /// One entry per distinct TagId covered in the session.
    /// </summary>
    public IReadOnlyList<TopicGradeResult> PerTagResults { get; init; } = [];

    public DateTime GradedAt { get; init; }
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