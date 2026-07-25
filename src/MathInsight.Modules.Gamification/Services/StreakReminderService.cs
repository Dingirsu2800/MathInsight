using MathInsight.Modules.Gamification.Persistence;
using MathInsight.Shared.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Gamification.Services;

/// <summary>
/// BR-45. Scans this module's StudyStreak rows for students who have not recorded a qualifying
/// activity today and publishes a StreakReminderEvent for each. Read-only over the DB; the actual
/// push is the Notification module's job (008).
/// </summary>
public class StreakReminderService : IStreakReminderService
{
    private readonly GamificationDbContext _dbContext;
    private readonly IMediator _mediator;

    public StreakReminderService(GamificationDbContext dbContext, IMediator mediator)
    {
        _dbContext = dbContext;
        _mediator = mediator;
    }

    public async Task<int> SendRemindersAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        // "Inactive today" = has a streak row but its last qualifying activity is null or before
        // today. Scan ONLY StudyStreak (this module's own table); never reach into other modules'
        // student tables. A student with no StudyStreak row at all has never had a qualifying
        // activity, so there is no streak to remind them about.
        var inactive = await _dbContext.StudyStreaks
            .AsNoTracking()
            .Where(streak => streak.LastActivityDate == null || streak.LastActivityDate < today)
            .Select(streak => new StreakReminderEvent(
                streak.StudentId,
                streak.CurrentStreak,
                streak.LastActivityDate))
            .ToListAsync(cancellationToken);

        foreach (var reminder in inactive)
        {
            await _mediator.Publish(reminder, cancellationToken);
        }

        return inactive.Count;
    }
}
