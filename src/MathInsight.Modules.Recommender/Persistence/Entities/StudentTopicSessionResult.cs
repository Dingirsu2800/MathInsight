namespace MathInsight.Modules.Recommender.Persistence.Entities;

/// <summary>
/// Per-session per-topic snapshot used to update TagsMastery.
/// Required for audit and idempotency (RCM-08).
/// Owned by Recommender module. Maps to DB script table: StudentTopicSessionResult.
/// Unique constraint: (session_id, tag_id).
/// </summary>
public class StudentTopicSessionResult
{
    public Guid StudentTopicSessionResultId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SessionId { get; set; }
    public Guid TagId { get; set; }

    public int TotalQuestions { get; set; }
    public int CorrectCount { get; set; }
    public int WrongCount { get; set; }

    /// <summary>Per-topic score in range 0.00..10.00.</summary>
    public decimal TopicScore { get; set; }

    /// <summary>TagsMastery.OfficialPoint before this session was applied.</summary>
    public decimal PointBefore { get; set; }

    /// <summary>TagsMastery.OfficialPoint after this session was applied.</summary>
    public decimal PointAfter { get; set; }

    public DateTime CreatedTime { get; set; }
}
