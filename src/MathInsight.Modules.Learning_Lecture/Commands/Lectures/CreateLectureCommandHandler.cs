using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MathInsight.Modules.Learning_Lecture.Contracts;
using MathInsight.Modules.Learning_Lecture.Entities;
using MathInsight.Modules.Learning_Lecture.Persistence;

namespace MathInsight.Modules.Learning_Lecture.Commands.Lectures;

public class CreateLectureCommandHandler : IRequestHandler<CreateLectureCommand, LectureDto>
{
    private readonly LearningDbContext _dbContext;

    public CreateLectureCommandHandler(LearningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<LectureDto> Handle(CreateLectureCommand request, CancellationToken cancellationToken)
    {
        // Validation: TagId existence should technically be validated, assuming it is valid for MVP or validated at gateway.
        
        var lecture = new Lecture
        {
            LectureId = Guid.NewGuid().ToString(),
            Title = request.Title,
            Content = request.Content,
            VideoUrl = request.VideoUrl,
            ThumbnailUrl = request.ThumbnailUrl,
            TagId = request.TagId,
            TeacherId = request.TeacherId,
            Status = "Draft",
            CreatedTime = DateTime.UtcNow,
            UpdatedTime = DateTime.UtcNow,
            Likes = 0
        };

        if (request.MaterialIds != null)
        {
            foreach (var mid in request.MaterialIds)
            {
                lecture.LectureMaterials.Add(new LectureMaterial { LectureId = lecture.LectureId, MaterialId = mid });
            }
        }

        _dbContext.Lectures.Add(lecture);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new LectureDto 
        { 
            LectureId = lecture.LectureId, 
            Title = lecture.Title, 
            Status = lecture.Status,
            TeacherId = lecture.TeacherId,
            TagId = lecture.TagId,
            CreatedTime = lecture.CreatedTime,
            UpdatedTime = lecture.UpdatedTime
        };
    }
}
