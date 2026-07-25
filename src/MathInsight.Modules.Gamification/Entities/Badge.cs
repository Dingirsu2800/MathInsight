using MathInsight.Modules.Gamification.Enums;

namespace MathInsight.Modules.Gamification.Entities;

/// <summary>
/// Badge catalogue entry (BR-43). Maps to DB table [Badge]. Awarded to a student via
/// <see cref="StudentBadge"/> when ConditionValue is met for the given ConditionType.
/// </summary>
public class Badge
{
    public string BadgeId { get; set; } = default!;
    public string BadgeName { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string? IconUrl { get; set; }
    public ConditionType ConditionType { get; set; }
    public int ConditionValue { get; set; }
    public DateTime CreatedTime { get; set; }
}
