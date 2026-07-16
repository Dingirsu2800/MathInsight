using MediatR;

namespace MathInsight.Modules.Learning_Lecture.Events;

public record ActivityLoggedEvent(
    string StudentId,
    string ActivityType,
    string? LectureId,
    string? MaterialId,
    int DurationSeconds
) : INotification;
