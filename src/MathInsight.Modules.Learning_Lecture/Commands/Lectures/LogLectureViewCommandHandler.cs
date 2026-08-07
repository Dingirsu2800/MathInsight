using MathInsight.Modules.Learning_Lecture.Persistence;
using MathInsight.Shared.Events;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace MathInsight.Modules.Learning_Lecture.Commands.Lectures;

public class LogLectureViewCommandHandler : IRequestHandler<LogLectureViewCommand, Result<bool>>
{
    private readonly LearningDbContext _dbContext;
    private readonly IMediator _mediator;

    public LogLectureViewCommandHandler(LearningDbContext dbContext, IMediator mediator)
    {
        _dbContext = dbContext;
        _mediator = mediator;
    }

    public async Task<Result<bool>> Handle(LogLectureViewCommand request, CancellationToken cancellationToken)
    {
        var lecture = await _dbContext.Lectures
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.LectureId == request.LectureId, cancellationToken);

        if (lecture == null || lecture.Status != "Published")
        {
            return Result<bool>.Failure(new Error("LectureNotFoundOrNotPublished", "Bài giảng không tồn tại hoặc chưa được xuất bản."));
        }

        // Publish ActivityLoggedEvent directly. Gamification module handles persistence in ActivityLog.
        await _mediator.Publish(new ActivityLoggedEvent(
            StudentId: request.StudentId,
            ActivityType: "VIEW_LECTURE",
            LectureId: request.LectureId,
            MaterialId: null,
            DurationSeconds: request.DurationSeconds
        ), cancellationToken);

        return Result<bool>.Success(true);
    }
}
