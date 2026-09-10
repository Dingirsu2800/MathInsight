using MathInsight.Modules.Notification_Report.Services;
using MathInsight.Shared.Events;
using MediatR;

namespace MathInsight.Modules.Notification_Report.Handlers;

/// <summary>UC-89. Pushes a "test graded" notification to the student.</summary>
public sealed class GradeCalculatedHandler : INotificationHandler<GradeCalculatedEvent>
{
    private readonly INotificationService _notificationService;

    public GradeCalculatedHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public Task Handle(GradeCalculatedEvent notification, CancellationToken cancellationToken)
    {
        var isAdjustment = string.Equals(
            notification.Cause,
            GradeCalculatedEvent.ScoreAdjustmentCause,
            StringComparison.Ordinal);

        return _notificationService.SendAsync(
            notification.StudentId,
            isAdjustment ? "Score Adjusted" : "Test Graded",
            isAdjustment
                ? $"Your score was adjusted: {notification.Score:0.##}/10."
                : $"Your test has been graded: {notification.Score:0.##}/10.",
            $"/student/test-result/{notification.SessionId}",
            cancellationToken,
            isAdjustment
                ? $"score-adjustment:{notification.IncidentId ?? notification.ReportId}:{notification.SessionId}:{notification.GradeRevision}"
                : null);
    }
}
