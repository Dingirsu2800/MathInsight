using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Learning_Lecture.Contracts;
using MathInsight.Modules.Learning_Lecture.Persistence;
using MathInsight.Modules.Learning_Lecture.Events;

namespace MathInsight.Modules.Learning_Lecture.Queries.Lectures;

public class GetLectureQueryHandler : IRequestHandler<GetLectureQuery, LectureDto>
{
    private readonly LearningDbContext _dbContext;
    private readonly IMediator _mediator;

    public GetLectureQueryHandler(LearningDbContext dbContext, IMediator mediator)
    {
        _dbContext = dbContext;
        _mediator = mediator;
    }

    public async Task<LectureDto> Handle(GetLectureQuery request, CancellationToken cancellationToken)
    {
        var lecture = await _dbContext.Lectures
            .AsNoTracking()
            .Include(x => x.LectureMaterials)
                .ThenInclude(lm => lm.Material)
            .FirstOrDefaultAsync(x => x.LectureId == request.LectureId, cancellationToken);

        if (lecture == null) throw new Exception("Lecture not found");

        if (!string.IsNullOrEmpty(request.StudentId) && lecture.Status == "Published")
        {
            // Background event publish for activity logging
            _ = _mediator.Publish(new ActivityLoggedEvent(
                StudentId: request.StudentId,
                ActivityType: "VIEW_LECTURE",
                LectureId: request.LectureId,
                MaterialId: null,
                DurationSeconds: 0
            ), cancellationToken);
        }

        return new LectureDto
        {
            LectureId = lecture.LectureId,
            Title = lecture.Title,
            Content = lecture.Content,
            VideoUrl = lecture.VideoUrl,
            ThumbnailUrl = lecture.ThumbnailUrl,
            Likes = lecture.Likes,
            TeacherId = lecture.TeacherId,
            TagId = lecture.TagId,
            Status = lecture.Status,
            CreatedTime = lecture.CreatedTime,
            UpdatedTime = lecture.UpdatedTime,
            Materials = lecture.LectureMaterials
                .Where(lm => lm.Material.Status == "Active")
                .Select(lm => new MaterialDto
                {
                    MaterialId = lm.Material.MaterialId,
                    MaterialName = lm.Material.MaterialName,
                    FileUrl = lm.Material.FileUrl,
                    FileType = lm.Material.FileType,
                    TeacherId = lm.Material.TeacherId,
                    Status = lm.Material.Status,
                    UploadedTime = lm.Material.UploadedTime
                }).ToList()
        };
    }
}
