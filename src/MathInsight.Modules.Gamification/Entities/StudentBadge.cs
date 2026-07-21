namespace MathInsight.Modules.Gamification.Entities;

/// <summary>
/// Award record linking a student to an earned badge. Composite PK (StudentID, BadgeID) makes
/// re-awarding impossible. Insert-only — no other columns, never updated or deleted.
/// Maps to DB table [StudentBadge].
/// </summary>
public class StudentBadge
{
    public string StudentId { get; set; } = default!;
    public string BadgeId { get; set; } = default!;
    public DateTime EarnedTime { get; set; }
}
