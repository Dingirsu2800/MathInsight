using MathInsight.Modules.Gamification.Entities;
using MathInsight.Modules.Gamification.Enums;
using MathInsight.Modules.Gamification.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

namespace MathInsight.Modules.Gamification.Queries.Badges;

public sealed class GetBadgeProgressQueryHandler : IRequestHandler<GetBadgeProgressQuery, List<BadgeProgressDto>>
{
    private readonly GamificationDbContext _dbContext;

    public GetBadgeProgressQueryHandler(GamificationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<BadgeProgressDto>> Handle(GetBadgeProgressQuery request, CancellationToken cancellationToken)
    {
        // 1. Get all unearned badges
        var earnedBadgeIds = await _dbContext.Set<StudentBadge>()
            .Where(sb => sb.StudentId == request.StudentId)
            .Select(sb => sb.BadgeId)
            .ToListAsync(cancellationToken);

        var unearnedBadges = await _dbContext.Set<Badge>()
            .Where(b => !earnedBadgeIds.Contains(b.BadgeId))
            .ToListAsync(cancellationToken);

        if (!unearnedBadges.Any())
        {
            return new List<BadgeProgressDto>();
        }

        // 2. Get current values for the 3 conditions
        var streak = await _dbContext.Set<StudyStreak>()
            .FirstOrDefaultAsync(s => s.StudentId == request.StudentId, cancellationToken);
        int currentStreak = streak?.CurrentStreak ?? 0;

        int testsCompleted = await _dbContext.Set<ActivityLog>()
            .CountAsync(a => a.StudentId == request.StudentId && 
                             (a.ActivityType == ActivityType.PRACTICE || a.ActivityType == ActivityType.EXAM),
                        cancellationToken);

        int totalCorrect = await GetTotalCorrectAnswersAsync(request.StudentId, cancellationToken);

        // 3. Map to DTO
        var result = new List<BadgeProgressDto>();
        foreach (var badge in unearnedBadges)
        {
            int currentValue = badge.ConditionType switch
            {
                ConditionType.STREAK_DAYS => currentStreak,
                ConditionType.TESTS_COMPLETED => testsCompleted,
                ConditionType.TOTAL_CORRECT_ANSWERS => totalCorrect,
                _ => 0
            };

            var progress = new BadgeProgressDto
            {
                BadgeId = badge.BadgeId,
                BadgeName = badge.BadgeName,
                RequiredValue = badge.ConditionValue,
                CurrentValue = currentValue,
                ProgressPercentage = badge.ConditionValue > 0 
                    ? Math.Min(100, Math.Round((decimal)currentValue / badge.ConditionValue * 100, 2)) 
                    : 100
            };
            
            result.Add(progress);
        }

        return result;
    }

    private async Task<int> GetTotalCorrectAnswersAsync(string studentId, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = _dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM [TestAnswer] WHERE StudentID = @studentId AND IsCorrect = 1";
            
            var param = command.CreateParameter();
            param.ParameterName = "@studentId";
            param.Value = studentId;
            command.Parameters.Add(param);

            await _dbContext.Database.OpenConnectionAsync(cancellationToken);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            await _dbContext.Database.CloseConnectionAsync();

            return result != DBNull.Value && result != null ? Convert.ToInt32(result) : 0;
        }
        catch
        {
            return 0;
        }
    }
}
