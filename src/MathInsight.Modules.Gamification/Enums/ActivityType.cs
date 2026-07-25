namespace MathInsight.Modules.Gamification.Enums;

/// <summary>
/// Kind of learning activity recorded in ActivityLog (spec.md §Activity Types).
/// Persisted and compared as VARCHAR(50) by enum NAME, so the member names must match the
/// exact string values written to the ActivityType column — do not rename or reorder loosely.
/// </summary>
public enum ActivityType
{
    VIEW_LECTURE,
    DOWNLOAD_MATERIAL,
    PRACTICE,
    EXAM
}
