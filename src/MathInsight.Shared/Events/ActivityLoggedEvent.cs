using MediatR;

namespace MathInsight.Shared.Events;

public record ActivityLoggedEvent(
    string StudentId,
    string ActivityType,
    string? LectureId,
    string? MaterialId,
    int DurationSeconds
) : INotification;
