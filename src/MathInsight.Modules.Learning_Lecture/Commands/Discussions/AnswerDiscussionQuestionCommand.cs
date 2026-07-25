using MediatR;
using MathInsight.Modules.Learning_Lecture.Contracts;

namespace MathInsight.Modules.Learning_Lecture.Commands.Discussions;

public record AnswerDiscussionQuestionCommand(
    string DiscussionQuestionId,
    string AccountId,
    string Content
) : IRequest<DiscussionAnswerDto>;
