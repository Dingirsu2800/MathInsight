using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Learning_Lecture.Persistence;
using MathInsight.Modules.Learning_Lecture.Entities;

namespace MathInsight.Modules.Learning_Lecture.Commands.Lectures;

public class LikeLectureCommandHandler : IRequestHandler<LikeLectureCommand, bool>
{
    private readonly LearningDbContext _dbContext;

    public LikeLectureCommandHandler(LearningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> Handle(LikeLectureCommand request, CancellationToken cancellationToken)
    {
        var lecture = await _dbContext.Lectures.FirstOrDefaultAsync(x => x.LectureId == request.LectureId, cancellationToken);
        if (lecture == null) throw new Exception("Lecture not found");
        if (lecture.Status != "Published") throw new Exception("Cannot like non-published lecture");

        var existingLike = await _dbContext.LectureLikes
            .FirstOrDefaultAsync(x => x.LectureId == request.LectureId && x.StudentId == request.StudentId, cancellationToken);
        
        if (existingLike != null) return true; // Idempotent success or throw conflict based on requirement.

        var like = new LectureLike
        {
            LectureId = request.LectureId,
            StudentId = request.StudentId,
            CreatedTime = DateTime.UtcNow
        };

        _dbContext.LectureLikes.Add(like);
        lecture.Likes += 1;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
