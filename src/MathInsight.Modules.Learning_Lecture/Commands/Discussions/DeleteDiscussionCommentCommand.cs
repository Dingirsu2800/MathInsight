using MediatR;

namespace MathInsight.Modules.Learning_Lecture.Commands.Discussions;

public record DeleteDiscussionCommentCommand(
    string Id,
    bool IsQuestion,
    string AccountId,
    bool IsTeacherOrAdmin
) : IRequest<bool>;
