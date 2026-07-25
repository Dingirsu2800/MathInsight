using MediatR;

namespace MathInsight.Modules.Learning_Lecture.Events;

public record DiscussionQuestionPostedEvent(
    string DiscussionQuestionId,
    string LectureId,
    string StudentId,
    string TeacherId,
    string Title
) : INotification;
