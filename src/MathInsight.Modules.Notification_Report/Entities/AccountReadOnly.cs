namespace MathInsight.Modules.Notification_Report.Entities;

/// <summary>
/// Read-only view of the Account table (owned by Identity_Access). Used by the Leaderboard query
/// to resolve a student's display name. Mirrors QuestionBank's AccountReadModel pattern.
/// </summary>
public class AccountReadOnly
{
    public string AccountId { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
}
