using MediatR;

namespace MathInsight.Modules.Gamification.Queries.Badges;

public sealed record GetBadgeListQuery(string StudentId) : IRequest<List<BadgeDto>>;

public sealed record BadgeDto
{
    public string BadgeId { get; init; } = default!;
    public string BadgeName { get; init; } = default!;
    public string Description { get; init; } = default!;
    public string? IconUrl { get; init; }
    public bool IsEarned { get; init; }
    public DateTime? EarnedTime { get; init; }
}
