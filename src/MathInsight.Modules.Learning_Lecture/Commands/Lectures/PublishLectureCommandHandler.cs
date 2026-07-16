using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Learning_Lecture.Persistence;

namespace MathInsight.Modules.Learning_Lecture.Commands.Lectures;

public class PublishLectureCommandHandler : IRequestHandler<PublishLectureCommand, bool>
{
    private readonly LearningDbContext _dbContext;

    public PublishLectureCommandHandler(LearningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> Handle(PublishLectureCommand request, CancellationToken cancellationToken)
    {
        var lecture = await _dbContext.Lectures.FirstOrDefaultAsync(x => x.LectureId == request.LectureId, cancellationToken);
        if (lecture == null) throw new Exception("Lecture not found");
        if (!request.IsAdmin && lecture.TeacherId != request.TeacherId) throw new Exception("Forbidden: Not the owner");
        if (lecture.Status == "Published") return true;
        if (lecture.Status != "Draft") throw new Exception("Only Draft lectures can be published");
        if (string.IsNullOrEmpty(lecture.VideoUrl) && string.IsNullOrEmpty(lecture.Content)) 
            throw new Exception("Lecture must have either VideoUrl or Content to be published");

        lecture.Status = "Published";
        lecture.UpdatedTime = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
