using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Learning_Lecture.Contracts;
using MathInsight.Modules.Learning_Lecture.Persistence;

namespace MathInsight.Modules.Learning_Lecture.Queries.Lectures;

public class GetLectureListQueryHandler : IRequestHandler<GetLectureListQuery, PagedResult<LectureDto>>
{
    private readonly LearningDbContext _dbContext;

    public GetLectureListQueryHandler(LearningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<LectureDto>> Handle(GetLectureListQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Lectures.AsNoTracking();

        if (request.IsStudent)
        {
            query = query.Where(x => x.Status == "Published");
        }
        else if (!string.IsNullOrEmpty(request.TeacherId))
        {
            query = query.Where(x => x.TeacherId == request.TeacherId);
        }

        if (!string.IsNullOrEmpty(request.Search))
        {
            query = query.Where(x => x.Title.Contains(request.Search));
        }
        
        if (!string.IsNullOrEmpty(request.Status))
        {
            query = query.Where(x => x.Status == request.Status);
        }

        if (!string.IsNullOrEmpty(request.Topic))
        {
            query = query.Where(x => x.TagId == request.Topic);
        }

        if (request.Grade.HasValue)
        {
            var validTagIds = _dbContext.TagTopics.Where(t => t.Grade == request.Grade.Value).Select(t => t.TagId);
            query = query.Where(x => validTagIds.Contains(x.TagId));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var skip = (request.Page - 1) * request.PageSize;

        var lectures = await query
            .OrderByDescending(x => x.CreatedTime)
            .Skip(skip)
            .Take(request.PageSize)
            .Select(x => new LectureDto
            {
                LectureId = x.LectureId,
                Title = x.Title,
                VideoUrl = x.VideoUrl,
                ThumbnailUrl = x.ThumbnailUrl,
                Likes = x.Likes,
                TeacherId = x.TeacherId,
                TagId = x.TagId,
                TagName = _dbContext.TagTopics.Where(t => t.TagId == x.TagId).Select(t => t.TagName).FirstOrDefault(),
                Status = x.Status,
                CreatedTime = x.CreatedTime,
                UpdatedTime = x.UpdatedTime
            }).ToListAsync(cancellationToken);

        return new PagedResult<LectureDto>(lectures, totalCount);
    }
}
