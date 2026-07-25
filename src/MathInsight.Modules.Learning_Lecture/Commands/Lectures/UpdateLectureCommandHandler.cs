using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Learning_Lecture.Contracts;
using MathInsight.Modules.Learning_Lecture.Persistence;

namespace MathInsight.Modules.Learning_Lecture.Commands.Lectures;

public class UpdateLectureCommandHandler : IRequestHandler<UpdateLectureCommand, LectureDto>
{
    private readonly LearningDbContext _dbContext;

    public UpdateLectureCommandHandler(LearningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<LectureDto> Handle(UpdateLectureCommand request, CancellationToken cancellationToken)
    {
        var lecture = await _dbContext.Lectures
            .Include(l => l.LectureMaterials)
            .FirstOrDefaultAsync(x => x.LectureId == request.LectureId, cancellationToken);
        if (lecture == null) throw new Exception("Lecture not found");
        if (lecture.TeacherId != request.TeacherId) throw new Exception("Forbidden: Not the owner");
        if (lecture.Status == "Deactivated") throw new Exception("Cannot update deactivated lecture");

        lecture.Title = request.Title;
        lecture.Content = request.Content;
        lecture.VideoUrl = request.VideoUrl;
        lecture.ThumbnailUrl = request.ThumbnailUrl;
        lecture.TagId = request.TagId;
        lecture.UpdatedTime = DateTime.UtcNow;

        if (request.MaterialIds != null)
        {
            _dbContext.LectureMaterials.RemoveRange(lecture.LectureMaterials);
            foreach (var mid in request.MaterialIds)
            {
                lecture.LectureMaterials.Add(new MathInsight.Modules.Learning_Lecture.Entities.LectureMaterial { LectureId = lecture.LectureId, MaterialId = mid });
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new LectureDto 
        { 
            LectureId = lecture.LectureId, 
            Title = lecture.Title, 
            Status = lecture.Status 
        };
    }
}
