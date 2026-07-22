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

    /// <summary>Current grading revision. Increases on re-grade (e.g., after report invalidation).</summary>
    public int GradeRevision { get; init; }

    public DateTime GradedAt { get; init; }
}

/// <summary>
/// Detailed answer info for Elo calculation.
/// Unified Multi-Tag v4.1: includes TagWeights for multi-tag delta distribution.
/// </summary>
public sealed record GradedAnswerDto
{
    public Guid QuestionId { get; init; }

    /// <summary>
    /// Primary topic tag ID (backward-compatible). Always the IsPrimary=true tag.
    /// </summary>
    public Guid TagId { get; init; }

    /// <summary>
    /// All tags (primary + secondary) with their role-based weights.
    /// Sum of all weights = 1.0. For single-tag questions, contains one entry with Weight = 1.0.
    /// Used by Recommender for multi-tag Elo delta distribution (Çông thức 2, Bước 2).
    /// Weight rules (BR-13/14/15):
    ///   - Single tag: w = 1.0
    ///   - Tag Chính (primary): w_main ∈ [0.60, 0.70], default 0.65
    ///   - Tag Phụ (secondary): w_sub_i = (1 − w_main) / N_sub
    /// </summary>
    public IReadOnlyList<TagWeightEntry> TagWeights { get; init; } = [];

    /// <summary>
    /// Normalized question score on 0–10 scale: s_q = PointsEarned / MaxPoints × 10.0.
    /// Used for Tầng 1 contribution calculation: c_{q,i} = s_q × w_{iq}.
    /// </summary>
    public decimal NormalizedScore { get; init; }

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

    /// <summary>
    /// True when the question was invalidated due to a confirmed report.
    /// Effective points = MaxPoints when invalidated.
    /// </summary>
    public bool IsScoreInvalidated { get; init; }
}

/// <summary>
/// Represents a question's tag assignment with its weight for multi-tag support.
/// </summary>
public sealed record TagWeightEntry
{
    public Guid TagId { get; init; }

    /// <summary>
    /// Role-based weight w_{iq}. Sum of all TagWeightEntry.Weight for a question = 1.0.
    /// </summary>
    public decimal Weight { get; init; }

    public bool IsPrimary { get; init; }
}

/// <summary>
/// Per-topic grading result snapshot for a single (SessionId, TagId) pair.
/// Used by Recommender to update TagsMastery and insert StudentTopicSessionResult.
///
/// Unified Multi-Tag v4.1: TopicScore is now calculated using the weighted Tầng 1–2 formula:
///   Tầng 1: c_{q,i} = s_q × w_{iq} (contribution of question q to tag i)
///   Tầng 2: T_j^{(i)} = avg(c_{q,i}) across all questions in session j containing tag i
/// For single-tag questions (w=1.0), T_j^{(i)} = avg(s_q) = traditional formula.
/// PerTagResults now includes entries for ALL tags (primary + secondary), not just primary.
/// </summary>
public sealed record TopicGradeResult
{
    public Guid TagId { get; init; }

    /// <summary>
    /// Weighted topic score 0.00–10.00: T_j^{(i)} = avg(c_{q,i}) where c_{q,i} = s_q × w_{iq}.
    /// For single-tag questions, this equals the traditional correct_count / total_count × 10.0.
    /// </summary>
    public decimal TopicScore { get; init; }

    public int CorrectCount { get; init; }
    public int TotalCount { get; init; }
}