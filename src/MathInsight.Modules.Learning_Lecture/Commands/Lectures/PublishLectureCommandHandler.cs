using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Learning_Lecture.Errors;
using MathInsight.Modules.Learning_Lecture.Persistence;
using MathInsight.Shared.Results;

namespace MathInsight.Modules.Learning_Lecture.Commands.Lectures;

public class PublishLectureCommandHandler : IRequestHandler<PublishLectureCommand, Result<bool>>
{
    private readonly LearningDbContext _dbContext;

    public PublishLectureCommandHandler(LearningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<bool>> Handle(PublishLectureCommand request, CancellationToken cancellationToken)
    {
        var lecture = await _dbContext.Lectures.FirstOrDefaultAsync(x => x.LectureId == request.LectureId, cancellationToken);
        if (lecture is null) return Result<bool>.Failure(LearningErrors.LectureNotFound);
        if (!request.IsAdmin && lecture.TeacherId != request.TeacherId) return Result<bool>.Failure(LearningErrors.LectureForbidden);
        if (lecture.Status == "Published") return Result<bool>.Success(true);
        if (lecture.Status != "Draft") return Result<bool>.Failure(LearningErrors.LecturePublishStateInvalid);
        if (string.IsNullOrEmpty(lecture.VideoUrl) && string.IsNullOrEmpty(lecture.Content)) 
            return Result<bool>.Failure(LearningErrors.LectureContentRequired);

        var validationError = await LectureTaxonomyValidator.ValidateDifficultyAsync(
            _dbContext,
            lecture.DifficultyId,
            cancellationToken);

        if (validationError is not null)
            return Result<bool>.Failure(validationError);

        lecture.Status = "Published";
        lecture.UpdatedTime = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
