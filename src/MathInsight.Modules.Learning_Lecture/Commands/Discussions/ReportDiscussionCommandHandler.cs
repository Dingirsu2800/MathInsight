using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MathInsight.Modules.Learning_Lecture.Contracts;
using MathInsight.Modules.Learning_Lecture.Entities;
using MathInsight.Modules.Learning_Lecture.Persistence;

namespace MathInsight.Modules.Learning_Lecture.Commands.Discussions;

public class ReportDiscussionCommandHandler : IRequestHandler<ReportDiscussionCommand, DiscussionReportDto>
{
    private readonly LearningDbContext _dbContext;

    public ReportDiscussionCommandHandler(LearningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DiscussionReportDto> Handle(ReportDiscussionCommand request, CancellationToken cancellationToken)
    {
        if ((request.DiscussionQuestionId != null && request.DiscussionAnswerId != null) ||
            (request.DiscussionQuestionId == null && request.DiscussionAnswerId == null))
        {
            throw new Exception("Exactly one of DiscussionQuestionId or DiscussionAnswerId must be non-null");
        }

        var report = new DiscussionReport
        {
            ReportId = Guid.NewGuid().ToString(),
            DiscussionQuestionId = request.DiscussionQuestionId,
            DiscussionAnswerId = request.DiscussionAnswerId,
            ReporterAccountId = request.ReporterAccountId,
            ReportReason = request.Reason,
            Status = "Pending",
            CreatedTime = DateTime.UtcNow
        };

        _dbContext.DiscussionReports.Add(report);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new DiscussionReportDto
        {
            ReportId = report.ReportId,
            DiscussionQuestionId = report.DiscussionQuestionId,
            DiscussionAnswerId = report.DiscussionAnswerId,
            ReporterAccountId = report.ReporterAccountId,
            ReporterName = report.ReporterAccountId, // Simplified for now
            ReportReason = report.ReportReason,
            Status = report.Status,
            CreatedTime = report.CreatedTime,
            TargetType = request.DiscussionQuestionId != null ? "Question" : "Answer",
            TargetPreview = "",
            LectureTitle = ""
        };
    }
}
