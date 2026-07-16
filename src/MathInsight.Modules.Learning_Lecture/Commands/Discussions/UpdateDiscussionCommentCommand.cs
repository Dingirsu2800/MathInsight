using MediatR;

namespace MathInsight.Modules.Learning_Lecture.Commands.Discussions;

public record UpdateDiscussionCommentCommand(
    string Id,
    bool IsQuestion,
    string AccountId,
    string Content,
    bool IsTeacherOrAdmin
) : IRequest<bool>;
