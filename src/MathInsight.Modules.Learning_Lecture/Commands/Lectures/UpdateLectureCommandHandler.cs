using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Learning_Lecture.Contracts;
using MathInsight.Modules.Learning_Lecture.Errors;
using MathInsight.Modules.Learning_Lecture.Persistence;
using MathInsight.Shared.Results;

namespace MathInsight.Modules.Learning_Lecture.Commands.Lectures;

public class UpdateLectureCommandHandler : IRequestHandler<UpdateLectureCommand, Result<LectureDto>>
{
    private readonly LearningDbContext _dbContext;

    public UpdateLectureCommandHandler(LearningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<LectureDto>> Handle(UpdateLectureCommand request, CancellationToken cancellationToken)
    {
        var lecture = await _dbContext.Lectures
            .Include(l => l.LectureMaterials)
            .FirstOrDefaultAsync(x => x.LectureId == request.LectureId, cancellationToken);
        if (lecture is null) return Result<LectureDto>.Failure(LearningErrors.LectureNotFound);
        if (lecture.TeacherId != request.TeacherId) return Result<LectureDto>.Failure(LearningErrors.LectureForbidden);
        if (lecture.Status == "Deactivated") return Result<LectureDto>.Failure(LearningErrors.LectureCannotUpdateDeactivated);

        var validationError = await LectureTaxonomyValidator.ValidateAssignmentAsync(
            _dbContext,
            request.TagId,
            request.DifficultyId,
            cancellationToken);

        if (validationError is not null)
            return Result<LectureDto>.Failure(validationError);

        lecture.Title = request.Title;
        lecture.Content = request.Content;
        lecture.VideoUrl = request.VideoUrl;
        lecture.ThumbnailUrl = request.ThumbnailUrl;
        lecture.TagId = request.TagId;
        lecture.DifficultyId = request.DifficultyId;
        lecture.NextLectureId = request.NextLectureId;
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

        return Result<LectureDto>.Success(new LectureDto
        {
            LectureId = lecture.LectureId, 
            Title = lecture.Title, 
            Status = lecture.Status,
            DifficultyId = lecture.DifficultyId
        });
    }
}
