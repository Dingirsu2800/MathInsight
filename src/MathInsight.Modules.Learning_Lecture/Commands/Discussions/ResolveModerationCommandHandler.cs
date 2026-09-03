using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Learning_Lecture.Persistence;

using MathInsight.Modules.Learning_Lecture.Events;

namespace MathInsight.Modules.Learning_Lecture.Commands.Discussions;

public class ResolveModerationCommandHandler : IRequestHandler<ResolveModerationCommand, bool>
{
    private readonly LearningDbContext _dbContext;
    private readonly IPublisher _publisher;

    public ResolveModerationCommandHandler(LearningDbContext dbContext, IPublisher publisher)
    {
        _dbContext = dbContext;
        _publisher = publisher;
    }

    public async Task<bool> Handle(ResolveModerationCommand request, CancellationToken cancellationToken)
    {
        var report = await _dbContext.DiscussionReports
            .Include(x => x.Question)
            .Include(x => x.Answer)
            .FirstOrDefaultAsync(x => x.ReportId == request.ReportId, cancellationToken);
            
        if (report == null) throw new Exception("Report not found");

        report.Status = request.IsDismissed ? "Dismissed" : "Resolved";
        report.ResolvedTime = DateTime.UtcNow;
        report.ResolverAccountId = request.ResolverAccountId;

        if (!request.IsDismissed)
        {
            string? targetAccountId = null;
            string? lectureId = null;
            bool isQuestion = false;

            if (report.DiscussionQuestionId != null && report.Question != null)
            {
                report.Question.Status = "Hidden";
                report.Question.ModerationReason = request.Reason;
                targetAccountId = report.Question.StudentId;
                lectureId = report.Question.LectureId;
                isQuestion = true;
            }
            else if (report.DiscussionAnswerId != null && report.Answer != null)
            {
                report.Answer.Status = "Hidden";
                report.Answer.ModerationReason = request.Reason;
                targetAccountId = report.Answer.AccountId;
                if (report.Question != null)
                {
                    lectureId = report.Question.LectureId;
                }
                isQuestion = false;
            }

            if (targetAccountId != null && lectureId != null)
            {
                await _publisher.Publish(new DiscussionCommentHiddenEvent(
                    targetAccountId,
                    lectureId,
                    isQuestion,
                    request.Reason
                ), cancellationToken);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
