namespace MathInsight.Modules.Notification_Report.Entities;

/// <summary>
/// Read-only view of the CompetencyPoint table (owned by Recommender). Used by the Leaderboard
/// query (UC-58) to rank students per grade. Mirrors Recommender's own StudentReadOnly pattern —
/// no navigation, no FK, no migrations.
/// </summary>
public class CompetencyPointReadOnly
{
    public string CompetencyId { get; set; } = default!;
    public string StudentId { get; set; } = default!;
    public int Grade { get; set; }
    public decimal Point { get; set; }
}
