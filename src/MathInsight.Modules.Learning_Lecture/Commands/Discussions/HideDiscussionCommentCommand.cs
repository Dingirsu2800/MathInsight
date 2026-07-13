using MediatR;

namespace MathInsight.Modules.Learning_Lecture.Commands.Discussions;

public record HideDiscussionCommentCommand(string Id, bool IsQuestion, string TeacherOrAdminId) : IRequest<bool>;
