namespace MathInsight.Modules.Recommender.Persistence.Entities;

/// <summary>
/// Per-session per-topic snapshot used to update TagsMastery.
/// Required for audit and idempotency (RCM-08).
/// Owned by Recommender module. Maps to DB script table: StudentTopicSessionResult.
/// Unique constraint: (session_id, tag_id).
/// </summary>
public class StudentTopicSessionResult
{
    public string StudentTopicSessionResultId { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string TagId { get; set; } = string.Empty;

    public decimal TotalItems { get; set; }
    public decimal CorrectItems { get; set; }
    public decimal EarnedPoints { get; set; }
    public decimal MaxPoints { get; set; }

    /// <summary>Per-topic score in range 0.00..10.00.</summary>
    public decimal TopicScore { get; set; }

    public DateTime CreatedTime { get; set; }
}
