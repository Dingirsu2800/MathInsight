using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Learning_Lecture.Persistence;

namespace MathInsight.Modules.Learning_Lecture.Commands.Discussions;

public class ResolveModerationCommandHandler : IRequestHandler<ResolveModerationCommand, bool>
{
    private readonly LearningDbContext _dbContext;

    public ResolveModerationCommandHandler(LearningDbContext dbContext)
    {
        _dbContext = dbContext;
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
            if (report.DiscussionQuestionId != null && report.Question != null)
            {
                report.Question.Status = "Hidden";
            }
            else if (report.DiscussionAnswerId != null && report.Answer != null)
            {
                report.Answer.Status = "Hidden";
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
