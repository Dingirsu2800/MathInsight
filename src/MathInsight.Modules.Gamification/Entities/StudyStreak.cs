namespace MathInsight.Modules.Gamification.Entities;

/// <summary>
/// One row per student (StudentID is UNIQUE, 1:1). Tracks the consecutive-day study streak
/// (BR-39..BR-42). Maps to DB table [StudyStreak]. LastActivityDate is a DATE column.
/// </summary>
public class StudyStreak
{
    public string StreakId { get; set; } = default!;
    public string StudentId { get; set; } = default!;
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public DateOnly? LastActivityDate { get; set; }
}
