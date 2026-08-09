using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Learning_Lecture.Contracts;
using MathInsight.Modules.Learning_Lecture.Entities;
using MathInsight.Modules.Learning_Lecture.Persistence;
using MathInsight.Shared.Results;

namespace MathInsight.Modules.Learning_Lecture.Commands.Lectures;

public class CreateLectureCommandHandler : IRequestHandler<CreateLectureCommand, Result<LectureDto>>
{
    private readonly LearningDbContext _dbContext;

    public CreateLectureCommandHandler(LearningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<LectureDto>> Handle(CreateLectureCommand request, CancellationToken cancellationToken)
    {
        var validationError = await LectureTaxonomyValidator.ValidateAssignmentAsync(
            _dbContext,
            request.TagId,
            request.DifficultyId,
            cancellationToken);

        if (validationError is not null)
            return Result<LectureDto>.Failure(validationError);

        var lecture = new Lecture
        {
            LectureId = Guid.NewGuid().ToString(),
            Title = request.Title,
            Content = request.Content,
            VideoUrl = request.VideoUrl,
            ThumbnailUrl = request.ThumbnailUrl,
            TagId = request.TagId,
            DifficultyId = request.DifficultyId,
            TeacherId = request.TeacherId,
            Status = "Draft",
            CreatedTime = DateTime.UtcNow,
            UpdatedTime = DateTime.UtcNow,
            Likes = 0,
            NextLectureId = request.NextLectureId
        };

        if (request.MaterialIds != null && request.MaterialIds.Any())
        {
            var materials = await _dbContext.Materials
                .Where(m => request.MaterialIds.Contains(m.MaterialId))
                .ToListAsync(cancellationToken);

            var duplicateName = materials
                .GroupBy(m => m.MaterialName)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicateName != null)
            {
                throw new InvalidOperationException($"PRD Violation: Lecture cannot contain multiple materials with the same name '{duplicateName.Key}'.");
            }

            foreach (var mid in request.MaterialIds)
            {
                lecture.LectureMaterials.Add(new LectureMaterial { LectureId = lecture.LectureId, MaterialId = mid });
            }
        }

        _dbContext.Lectures.Add(lecture);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result<LectureDto>.Success(new LectureDto
        {
            LectureId = lecture.LectureId, 
            Title = lecture.Title, 
            Status = lecture.Status,
            TeacherId = lecture.TeacherId,
            TagId = lecture.TagId,
            DifficultyId = lecture.DifficultyId,
            CreatedTime = lecture.CreatedTime,
            UpdatedTime = lecture.UpdatedTime
        });
    }
}
