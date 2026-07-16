using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Learning_Lecture.Contracts;
using MathInsight.Modules.Learning_Lecture.Persistence;

namespace MathInsight.Modules.Learning_Lecture.Queries.Discussions;

public class GetModerationQueueQueryHandler : IRequestHandler<GetModerationQueueQuery, List<DiscussionReportDto>>
{
    private readonly LearningDbContext _dbContext;

    public GetModerationQueueQueryHandler(LearningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<DiscussionReportDto>> Handle(GetModerationQueueQuery request, CancellationToken cancellationToken)
    {
        var skip = (request.Page - 1) * request.PageSize;

        var query = _dbContext.DiscussionReports
            .AsNoTracking();

        if (!string.IsNullOrEmpty(request.TeacherId))
        {
            query = query.Where(x => 
                (x.Question != null && x.Question.Lecture.TeacherId == request.TeacherId) ||
                (x.Answer != null && x.Answer.Question.Lecture.TeacherId == request.TeacherId));
        }

        var reports = await query
            .OrderBy(x => x.CreatedTime)
            .Skip(skip)
            .Take(request.PageSize)
            .Select(x => new DiscussionReportDto
            {
                ReportId = x.ReportId,
                DiscussionQuestionId = x.DiscussionQuestionId,
                DiscussionAnswerId = x.DiscussionAnswerId,
                ReporterAccountId = x.ReporterAccountId,
                ReporterName = x.ReporterAccountId,
                ReportReason = x.ReportReason,
                Status = x.Status,
                CreatedTime = x.CreatedTime,
                TargetType = x.DiscussionQuestionId != null ? "Question" : "Answer",
                TargetPreview = x.DiscussionQuestionId != null ? x.Question!.Content : x.Answer!.Content,
                LectureTitle = x.DiscussionQuestionId != null ? x.Question!.Lecture.Title : x.Answer!.Question.Lecture.Title,
                ResolvedBy = x.ResolverAccountId,
                ResolvedAt = x.ResolvedTime
            }).ToListAsync(cancellationToken);

        return reports;
    }
}
