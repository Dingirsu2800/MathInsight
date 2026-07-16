using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Learning_Lecture.Contracts;
using MathInsight.Modules.Learning_Lecture.Persistence;

namespace MathInsight.Modules.Learning_Lecture.Queries.Discussions;

public class GetDiscussionsQueryHandler : IRequestHandler<GetDiscussionsQuery, List<DiscussionQuestionDto>>
{
    private readonly LearningDbContext _dbContext;

    public GetDiscussionsQueryHandler(LearningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<DiscussionQuestionDto>> Handle(GetDiscussionsQuery request, CancellationToken cancellationToken)
    {
        var skip = (request.Page - 1) * request.PageSize;

        var query = _dbContext.DiscussionQuestions
            .AsNoTracking()
            .Include(x => x.Answers)
            .Where(x => x.LectureId == request.LectureId && x.Status != "Deleted");

        if (request.IsStudent)
        {
            query = query.Where(x => x.Status == "Active");
        }

        var questions = await query
            .OrderByDescending(x => x.CreatedTime)
            .Skip(skip)
            .Take(request.PageSize)
            .Select(x => new DiscussionQuestionDto
            {
                DiscussionQuestionId = x.DiscussionQuestionId,
                LectureId = x.LectureId,
                StudentId = x.StudentId,
                Title = x.Title,
                Content = x.Content,
                Status = x.Status,
                CreatedTime = x.CreatedTime,
                UpdatedTime = x.UpdatedTime,
                Answers = x.Answers
                    .Where(a => a.Status != "Deleted" && (!request.IsStudent || a.Status == "Active"))
                    .OrderBy(a => a.CreatedTime)
                    .Select(a => new DiscussionAnswerDto
                    {
                        DiscussionAnswerId = a.DiscussionAnswerId,
                        AccountId = a.AccountId,
                        Content = a.Content,
                        Status = a.Status,
                        CreatedTime = a.CreatedTime,
                        UpdatedTime = a.UpdatedTime
                    }).ToList()
            }).ToListAsync(cancellationToken);

        return questions;
    }
}
