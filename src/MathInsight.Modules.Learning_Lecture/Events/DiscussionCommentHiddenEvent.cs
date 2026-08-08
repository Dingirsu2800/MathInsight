using MediatR;

namespace MathInsight.Modules.Learning_Lecture.Events;

/// <summary>
/// Event published when a teacher or admin hides a student's discussion question or answer.
/// </summary>
public record DiscussionCommentHiddenEvent(
    string TargetAccountId,
    string LectureId,
    bool IsQuestion,
    string? ModerationReason
) : INotification;
