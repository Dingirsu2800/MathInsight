using MathInsight.Modules.Gamification.Entities;
using MathInsight.Modules.Gamification.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Gamification.Queries.Badges;

public sealed class GetBadgeListQueryHandler : IRequestHandler<GetBadgeListQuery, List<BadgeDto>>
{
    private readonly GamificationDbContext _dbContext;

    public GetBadgeListQueryHandler(GamificationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<BadgeDto>> Handle(GetBadgeListQuery request, CancellationToken cancellationToken)
    {
        var allBadges = await _dbContext.Set<Badge>()
            .OrderBy(b => b.ConditionValue)
            .ToListAsync(cancellationToken);

        var earnedBadges = await _dbContext.Set<StudentBadge>()
            .Where(sb => sb.StudentId == request.StudentId)
            .ToDictionaryAsync(sb => sb.BadgeId, sb => sb.EarnedTime, cancellationToken);

        var result = allBadges.Select(b => new BadgeDto
        {
            BadgeId = b.BadgeId,
            BadgeName = b.BadgeName,
            Description = b.Description,
            IconUrl = b.IconUrl,
            IsEarned = earnedBadges.ContainsKey(b.BadgeId),
            EarnedTime = earnedBadges.TryGetValue(b.BadgeId, out var time) ? time : null
        }).ToList();

        return result;
    }
}
