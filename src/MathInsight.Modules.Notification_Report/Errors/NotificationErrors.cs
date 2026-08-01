using MathInsight.Shared.Results;

namespace MathInsight.Modules.Notification_Report.Errors;

public static class NotificationErrors
{
    public static readonly Error NotificationIdRequired = new(
        "NOTIFICATION_ID_REQUIRED",
        "Notification id is required.");

    public static readonly Error NotificationNotFound = new(
        "NOTIFICATION_NOT_FOUND",
        "Notification was not found.");

    public static readonly Error NotificationAccessForbidden = new(
        "NOTIFICATION_ACCESS_FORBIDDEN",
        "You are not allowed to access this notification.");

    public static readonly Error LeaderboardGradeInvalid = new(
        "LEADERBOARD_GRADE_INVALID",
        "Grade must be 10, 11, or 12.");
}
