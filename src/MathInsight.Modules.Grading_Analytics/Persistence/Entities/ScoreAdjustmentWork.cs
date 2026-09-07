namespace MathInsight.Modules.Grading_Analytics.Persistence.Entities;

/// <summary>
/// Durable post-commit delivery record for one recalculated session. The score mutation and this
/// record are committed together, so a process restart cannot lose the GradeCalculated event.
/// </summary>
public sealed class ScoreAdjustmentWork
{
    public string WorkId { get; set; } = string.Empty;
    public string ReportId { get; set; } = string.Empty;
    public string? IncidentId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public int GradeRevision { get; set; }
    public string EventPayload { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime? CompletedTime { get; set; }
}
