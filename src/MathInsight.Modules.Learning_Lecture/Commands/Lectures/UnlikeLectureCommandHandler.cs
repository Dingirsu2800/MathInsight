using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Learning_Lecture.Persistence;

namespace MathInsight.Modules.Learning_Lecture.Commands.Lectures;

public class UnlikeLectureCommandHandler : IRequestHandler<UnlikeLectureCommand, bool>
{
    private readonly LearningDbContext _dbContext;

    public UnlikeLectureCommandHandler(LearningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> Handle(UnlikeLectureCommand request, CancellationToken cancellationToken)
    {
        var like = await _dbContext.LectureLikes
            .FirstOrDefaultAsync(x => x.LectureId == request.LectureId && x.StudentId == request.StudentId, cancellationToken);
        
        if (like == null) return true; // Already unliked

        _dbContext.LectureLikes.Remove(like);
        
        var lecture = await _dbContext.Lectures.FirstOrDefaultAsync(x => x.LectureId == request.LectureId, cancellationToken);
        if (lecture != null && lecture.Likes > 0)
        {
            lecture.Likes -= 1;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
