using MediatR;

namespace MathInsight.Modules.Learning_Lecture.Events;

public record DiscussionAnsweredEvent(
    string DiscussionAnswerId,
    string DiscussionQuestionId,
    string LectureId,
    string AccountId,
    string StudentId
) : INotification;
