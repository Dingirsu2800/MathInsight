using MediatR;

namespace MathInsight.Modules.Gamification.Queries.Badges;

public sealed record GetBadgeProgressQuery(string StudentId) : IRequest<List<BadgeProgressDto>>;

public sealed record BadgeProgressDto
{
    public string BadgeId { get; init; } = default!;
    public string BadgeName { get; init; } = default!;
    public int RequiredValue { get; init; }
    public int CurrentValue { get; init; }
    public decimal ProgressPercentage { get; init; }
}
