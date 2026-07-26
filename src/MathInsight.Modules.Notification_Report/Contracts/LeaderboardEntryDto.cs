namespace MathInsight.Modules.Notification_Report.Contracts;

public sealed record LeaderboardEntryDto(
    int Rank,
    string StudentId,
    string StudentName,
    int Grade,
    decimal Point);
