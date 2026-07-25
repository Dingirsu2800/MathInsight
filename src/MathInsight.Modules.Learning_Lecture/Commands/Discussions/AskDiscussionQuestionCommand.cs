using MediatR;
using MathInsight.Modules.Learning_Lecture.Contracts;

namespace MathInsight.Modules.Learning_Lecture.Commands.Discussions;

public record AskDiscussionQuestionCommand(
    string LectureId,
    string StudentId,
    string Title,
    string Content
) : IRequest<DiscussionQuestionDto>;
