using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Learning_Lecture.Persistence;

namespace MathInsight.Modules.Learning_Lecture.Commands.Lectures;

public class DeactivateLectureCommandHandler : IRequestHandler<DeactivateLectureCommand, bool>
{
    private readonly LearningDbContext _dbContext;

    public DeactivateLectureCommandHandler(LearningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> Handle(DeactivateLectureCommand request, CancellationToken cancellationToken)
    {
        var lecture = await _dbContext.Lectures.FirstOrDefaultAsync(x => x.LectureId == request.LectureId, cancellationToken);
        if (lecture == null) throw new Exception("Lecture not found");
        if (!request.IsAdmin && lecture.TeacherId != request.TeacherId) throw new Exception("Forbidden: Not the owner");
        if (lecture.Status != "Published") throw new Exception("Only Published lectures can be deactivated");

        lecture.Status = "Deactivated";
        lecture.UpdatedTime = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
