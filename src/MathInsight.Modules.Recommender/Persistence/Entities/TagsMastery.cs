namespace MathInsight.Modules.Recommender.Persistence.Entities;

/// <summary>
/// Tracks topic mastery at grain (student_id, tag_id).
/// Owned by Recommender module. Maps to DB script table: TagsMastery.
/// Unique constraint: (student_id, tag_id). No difficulty_id column.
/// </summary>
public class TagsMastery
{
    public string TagsMasteryId { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public string TagId { get; set; } = string.Empty;

    /// <summary>NotLearned | Learning | Mastered</summary>
    public string MasteryStatus { get; set; } = "NotLearned";

    public int NumberDone { get; set; }
    public int NumCorrect { get; set; }

    /// <summary>Accuracy percentage (0..100) derived from NumCorrect / NumberDone * 100.</summary>
    public decimal AccuracyRate { get; set; }

    /// <summary>
    /// Weighted composite: 0.7 * ExamAnchor + 0.3 * PracticePoint. Range 0.00..10.00.
    /// WeakTag = OfficialPoint &lt; 5.00.
    /// </summary>
    public decimal OfficialPoint { get; set; }

    /// <summary>Updated during practice/adaptive sessions. Range 0.00..10.00.</summary>
    public decimal PracticePoint { get; set; }

    /// <summary>Updated from official/graded sessions. Range 0.00..10.00.</summary>
    public decimal ExamAnchor { get; set; }

    /// <summary>JSON or serialized history used for exam anchor calculation (up to 5 recent results).</summary>
    public string? ExamHistory { get; set; }

    /// <summary>Number of practice answers in the current series. Resets at 10.</summary>
    public int SeriesAnswerCount { get; set; }

    /// <summary>
    /// Derived from OfficialPoint: 1 (0-2.99), 2 (3-4.99), 3 (5-7.49), 4 (7.50-10).
    /// </summary>
    public byte RecommendedDifficultyLevel { get; set; }

    public DateTime? LastCalculatedAt { get; set; }
    public DateTime? LastPracticedTime { get; set; }
}
