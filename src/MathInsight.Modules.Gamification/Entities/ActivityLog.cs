using MathInsight.Modules.Gamification.Enums;

namespace MathInsight.Modules.Gamification.Entities;

/// <summary>
/// Insert-only record of a student learning activity (BR-40). Maps to DB table [ActivityLog].
/// StudentID / TestSessionID / LectureID / MaterialID are plain scalars — the aggregates they
/// point at live in other modules and the DDL declares no FK constraints for them.
/// </summary>
public class ActivityLog
{
    public string ActivityLogId { get; set; } = default!;
    public string StudentId { get; set; } = default!;
    public ActivityType ActivityType { get; set; }
    public string? TestSessionId { get; set; }
    public string? LectureId { get; set; }
    public string? MaterialId { get; set; }
    public int? DurationSeconds { get; set; }
    public DateTime ActivityDate { get; set; }
}
