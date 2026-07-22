using MathInsight.Modules.Gamification.Entities;
using MathInsight.Modules.Gamification.Enums;
using MathInsight.Modules.Gamification.Persistence;
using MathInsight.Shared.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;

namespace MathInsight.Modules.Gamification.Services;

public sealed class BadgeService : IBadgeService
{
    private readonly GamificationDbContext _dbContext;
    private readonly IMediator _mediator;
    private readonly ILogger<BadgeService> _logger;

    public BadgeService(
        GamificationDbContext dbContext,
        IMediator mediator,
        ILogger<BadgeService> logger)
    {
        _dbContext = dbContext;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task CheckAndAwardBadgesAsync(string studentId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking badges for student {StudentId}", studentId);

        // 1. Load all badges
        var allBadges = await _dbContext.Set<Badge>().ToListAsync(cancellationToken);

        // 2. Load already earned badges
        var earnedBadgeIds = await _dbContext.Set<StudentBadge>()
            .Where(sb => sb.StudentId == studentId)
            .Select(sb => sb.BadgeId)
            .ToListAsync(cancellationToken);

        // 3. Find unearned badges
        var unearnedBadges = allBadges.Where(b => !earnedBadgeIds.Contains(b.BadgeId)).ToList();
        if (!unearnedBadges.Any())
        {
            return; // All badges earned
        }

        // 4. Gather stats
        var streak = await _dbContext.Set<StudyStreak>()
            .FirstOrDefaultAsync(s => s.StudentId == studentId, cancellationToken);
        int currentStreak = streak?.CurrentStreak ?? 0;

        int testsCompleted = await _dbContext.Set<ActivityLog>()
            .CountAsync(a => a.StudentId == studentId && 
                             (a.ActivityType == ActivityType.PRACTICE || a.ActivityType == ActivityType.EXAM),
                        cancellationToken);

        int totalCorrectAnswers = await GetTotalCorrectAnswersAsync(studentId, cancellationToken);

        // 5. Check conditions and award
        var newAwards = new List<StudentBadge>();
        foreach (var badge in unearnedBadges)
        {
            bool isMet = badge.ConditionType switch
            {
                ConditionType.STREAK_DAYS => currentStreak >= badge.ConditionValue,
                ConditionType.TESTS_COMPLETED => testsCompleted >= badge.ConditionValue,
                ConditionType.TOTAL_CORRECT_ANSWERS => totalCorrectAnswers >= badge.ConditionValue,
                _ => false
            };

            if (isMet)
            {
                var award = new StudentBadge
                {
                    StudentId = studentId,
                    BadgeId = badge.BadgeId,
                    EarnedTime = DateTime.UtcNow
                };
                
                newAwards.Add(award);
                _dbContext.Set<StudentBadge>().Add(award);
                
                _logger.LogInformation("Awarded badge {BadgeName} to student {StudentId}", badge.BadgeName, studentId);
            }
        }

        if (newAwards.Any())
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Publish events for each new badge
            foreach (var award in newAwards)
            {
                var badgeInfo = unearnedBadges.First(b => b.BadgeId == award.BadgeId);
                var badgeEvent = new BadgeAwardedEvent(
                    studentId,
                    badgeInfo.BadgeId,
                    badgeInfo.BadgeName,
                    award.EarnedTime
                );
                await _mediator.Publish(badgeEvent, cancellationToken);
            }
        }
    }

    private async Task<int> GetTotalCorrectAnswersAsync(string studentId, CancellationToken cancellationToken)
    {
        // Cross-read from Testing module using raw SQL because we are in a modular monolith sharing the same DB
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not cross-read TestAnswer for TOTAL_CORRECT_ANSWERS. Defaulting to 0.");
            return 0;
        }
    }
}
