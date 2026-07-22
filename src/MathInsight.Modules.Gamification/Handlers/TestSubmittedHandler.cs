using MathInsight.Modules.Gamification.Entities;
using MathInsight.Modules.Gamification.Enums;
using MathInsight.Modules.Gamification.Persistence;
using MathInsight.Modules.Gamification.Services;
using MathInsight.Shared.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MathInsight.Modules.Gamification.Handlers;

/// <summary>
/// UC-80. In-process MediatR handler for <see cref="TestSubmittedEvent"/> (published by Testing on
/// practice/exam submission). Records the activity (insert-only, BR-40), advances the streak
/// (PRACTICE/EXAM always qualify under BR-39), then checks badge awards (BR-43).
///
/// TestSubmittedEvent identifies the student and session with <see cref="System.Guid"/> values;
/// the ActivityLog columns are VARCHAR(36), so the GUIDs are stored via ToString() ("D" format,
/// 36 chars) — matching how the Recommender handler persists GradeCalculatedEvent's GUID ids.
/// </summary>
public sealed class TestSubmittedHandler : INotificationHandler<TestSubmittedEvent>
{
    private readonly GamificationDbContext _dbContext;
    private readonly IStreakService _streakService;
    private readonly IBadgeService _badgeService;
    private readonly ILogger<TestSubmittedHandler> _logger;

    public TestSubmittedHandler(
        GamificationDbContext dbContext,
        IStreakService streakService,
        IBadgeService badgeService,
        ILogger<TestSubmittedHandler> logger)
    {
        _dbContext = dbContext;
        _streakService = streakService;
        _badgeService = badgeService;
        _logger = logger;
    }

    public async Task Handle(TestSubmittedEvent notification, CancellationToken cancellationToken)
    {
        // TestFormat is "Practice" / "Exam" (case-insensitive). Anything else is unexpected — log
        // and return rather than throw.
        if (!TryMapFormat(notification.TestFormat, out var activityType))
        {
            _logger.LogWarning(
                "Ignoring TestSubmittedEvent with unrecognized TestFormat '{TestFormat}' for student {StudentId}.",
                notification.TestFormat,
                notification.StudentId);
            return;
        }

        var studentId = notification.StudentId;

        // SubmittedTime may be unset on a malformed event; fall back to now.
        var submittedTime = notification.SubmittedTime == default
            ? DateTime.UtcNow
            : notification.SubmittedTime;

        _dbContext.ActivityLogs.Add(new ActivityLog
        {
            ActivityLogId = Guid.NewGuid().ToString(),
            StudentId = studentId,
            ActivityType = activityType,
            TestSessionId = notification.SessionId,
            LectureId = null,
            MaterialId = null,
            DurationSeconds = null,
            ActivityDate = submittedTime
        });

        // Duration is irrelevant for PRACTICE/EXAM (they always qualify); the service ignores it.
        var activityDate = DateOnly.FromDateTime(submittedTime);
        await _streakService.UpdateStreakAsync(
            studentId,
            activityType,
            0,
            activityDate,
            cancellationToken);

        await _badgeService.CheckAndAwardBadgesAsync(studentId, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool TryMapFormat(string testFormat, out ActivityType activityType)
    {
        if (string.Equals(testFormat, "Practice", StringComparison.OrdinalIgnoreCase))
        {
            activityType = ActivityType.PRACTICE;
            return true;
        }

        if (string.Equals(testFormat, "Exam", StringComparison.OrdinalIgnoreCase))
        {
            activityType = ActivityType.EXAM;
            return true;
        }

        activityType = default;
        return false;
    }
}
