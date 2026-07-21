namespace MathInsight.Modules.Gamification.Entities;

/// <summary>
/// A student's target score for a single topic tag (BR-44: UNIQUE per StudentID × TagID).
/// TargetPoint is in [0, 10] (DC-04). Maps to DB table [TargetScore].
/// </summary>
public class TargetScore
{
    public string TargetId { get; set; } = default!;
    public string StudentId { get; set; } = default!;
    public string TagId { get; set; } = default!;
    public decimal TargetPoint { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}
