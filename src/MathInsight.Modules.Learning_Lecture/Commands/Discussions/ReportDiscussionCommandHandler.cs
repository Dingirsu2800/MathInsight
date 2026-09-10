using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MathInsight.Modules.Learning_Lecture.Contracts;
using MathInsight.Modules.Learning_Lecture.Entities;
using MathInsight.Modules.Learning_Lecture.Persistence;
using MathInsight.Modules.Learning_Lecture.Events;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Learning_Lecture.Commands.Discussions;

public class ReportDiscussionCommandHandler : IRequestHandler<ReportDiscussionCommand, DiscussionReportDto>
{
    private readonly LearningDbContext _dbContext;
    private readonly IPublisher _publisher;

    public ReportDiscussionCommandHandler(LearningDbContext dbContext, IPublisher publisher)
    {
        _dbContext = dbContext;
        _publisher = publisher;
    }

    public async Task<DiscussionReportDto> Handle(ReportDiscussionCommand request, CancellationToken cancellationToken)
    {
        if ((request.DiscussionQuestionId != null && request.DiscussionAnswerId != null) ||
            (request.DiscussionQuestionId == null && request.DiscussionAnswerId == null))
        {
            throw new Exception("Exactly one of DiscussionQuestionId or DiscussionAnswerId must be non-null");
        }

        var alreadyReported = await _dbContext.DiscussionReports
            .AnyAsync(r => r.ReporterAccountId == request.ReporterAccountId &&
                           ((request.DiscussionQuestionId != null && r.DiscussionQuestionId == request.DiscussionQuestionId) ||
                            (request.DiscussionAnswerId != null && r.DiscussionAnswerId == request.DiscussionAnswerId)),
                      cancellationToken);

        if (alreadyReported)
        {
            throw new Exception("Bạn đã báo cáo bình luận này rồi.");
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

        string? teacherId = null;
        string? lectureId = null;

        if (request.DiscussionQuestionId != null)
        {
            var q = await _dbContext.DiscussionQuestions
                .Include(x => x.Lecture)
                .FirstOrDefaultAsync(x => x.DiscussionQuestionId == request.DiscussionQuestionId, cancellationToken);
            if (q?.Lecture != null)
            {
                lectureId = q.LectureId;
                teacherId = q.Lecture.TeacherId;
            }
        }
        else if (request.DiscussionAnswerId != null)
        {
            var a = await _dbContext.DiscussionAnswers
                .Include(x => x.Question)
                .ThenInclude(q => q.Lecture)
                .FirstOrDefaultAsync(x => x.DiscussionAnswerId == request.DiscussionAnswerId, cancellationToken);
            if (a?.Question?.Lecture != null)
            {
                lectureId = a.Question.LectureId;
                teacherId = a.Question.Lecture.TeacherId;
            }
        }

        if (teacherId != null && lectureId != null)
        {
            await _publisher.Publish(new DiscussionReportedEvent(
                report.ReportId,
                teacherId,
                lectureId,
                request.DiscussionQuestionId != null ? "Question" : "Answer",
                request.ReporterAccountId,
                request.Reason
            ), cancellationToken);
        }

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
