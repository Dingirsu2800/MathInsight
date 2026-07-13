using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Learning_Lecture.Contracts;
using MathInsight.Modules.Learning_Lecture.Persistence;

namespace MathInsight.Modules.Learning_Lecture.Queries.Lectures;

public class GetLectureListQueryHandler : IRequestHandler<GetLectureListQuery, List<LectureDto>>
{
    private readonly LearningDbContext _dbContext;

    public GetLectureListQueryHandler(LearningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<LectureDto>> Handle(GetLectureListQuery request, CancellationToken cancellationToken)
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
                Status = x.Status,
                CreatedTime = x.CreatedTime,
                UpdatedTime = x.UpdatedTime
            }).ToListAsync(cancellationToken);

        return lectures;
    }
}
