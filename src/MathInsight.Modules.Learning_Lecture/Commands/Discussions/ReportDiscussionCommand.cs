using MediatR;
using MathInsight.Modules.Learning_Lecture.Contracts;

namespace MathInsight.Modules.Learning_Lecture.Commands.Discussions;

public record ReportDiscussionCommand(
    string? DiscussionQuestionId,
    string? DiscussionAnswerId,
    string ReporterAccountId,
    string Reason
) : IRequest<DiscussionReportDto>;
