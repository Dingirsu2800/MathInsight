using MathInsight.Modules.Gamification.Entities;
using MathInsight.Modules.Gamification.Enums;
using MathInsight.Modules.Gamification.Persistence;
using MathInsight.Modules.Gamification.Services;
using MathInsight.Shared.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MathInsight.Modules.Gamification.Handlers;

/// <summary>
/// UC-80. In-process MediatR handler for <see cref="ActivityLoggedEvent"/> (published by Learning
/// on lecture view / material download). Records the activity (insert-only, BR-40), advances the
/// streak (BR-39..BR-42 — the service decides qualification), then checks badge awards (BR-43).
///
/// The ActivityLog is always persisted even when the activity does not advance the streak (e.g.
/// DOWNLOAD_MATERIAL, or a short lecture view), which is why the handler owns the final SaveChanges.
/// </summary>
public sealed class ActivityLoggedHandler : INotificationHandler<ActivityLoggedEvent>
{
    private readonly GamificationDbContext _dbContext;
    private readonly IStreakService _streakService;
    private readonly IBadgeService _badgeService;
    private readonly ILogger<ActivityLoggedHandler> _logger;

    public ActivityLoggedHandler(
        GamificationDbContext dbContext,
        IStreakService streakService,
        IBadgeService badgeService,
        ILogger<ActivityLoggedHandler> logger)
    {
        _dbContext = dbContext;
        _streakService = streakService;
        _badgeService = badgeService;
        _logger = logger;
    }

    public async Task Handle(ActivityLoggedEvent notification, CancellationToken cancellationToken)
    {
        // The event carries ActivityType as a raw UPPER_SNAKE_CASE string. Reject anything that is
        // not a known enum value — log and return rather than throw (an unknown type must not break
        // the publisher's request).
        if (!Enum.TryParse<ActivityType>(notification.ActivityType, out var activityType)
            || !Enum.IsDefined(activityType))
        {
            _logger.LogWarning(
                "Ignoring ActivityLoggedEvent with unrecognized ActivityType '{ActivityType}' for student {StudentId}.",
                notification.ActivityType,
                notification.StudentId);
            return;
        }

        var occurredAt = DateTime.UtcNow;

        _dbContext.ActivityLogs.Add(new ActivityLog
        {
            ActivityLogId = Guid.NewGuid().ToString(),
            StudentId = notification.StudentId,
            ActivityType = activityType,
            TestSessionId = null,
            LectureId = notification.LectureId,
            MaterialId = notification.MaterialId,
            DurationSeconds = notification.DurationSeconds,
            ActivityDate = occurredAt
        });

        // The service applies BR-39 qualification itself; do not pre-filter here.
        var activityDate = DateOnly.FromDateTime(occurredAt);
        await _streakService.UpdateStreakAsync(
            notification.StudentId,
            activityType,
            notification.DurationSeconds,
            activityDate,
            cancellationToken);

        await _badgeService.CheckAndAwardBadgesAsync(notification.StudentId, cancellationToken);

        // Ensures the ActivityLog is persisted even when the streak service made no change.
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
