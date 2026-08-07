using MathInsight.Modules.Learning_Lecture.Errors;
using MathInsight.Modules.Learning_Lecture.Persistence;
using MathInsight.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Learning_Lecture.Commands.Lectures;

internal static class LectureTaxonomyValidator
{
    public static async Task<Error?> ValidateAssignmentAsync(
        LearningDbContext dbContext,
        string tagId,
        string? difficultyId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(difficultyId))
            return LearningErrors.LectureDifficultyRequired;

        var topic = await dbContext.TagTopics
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.TagId == tagId, cancellationToken);

        if (topic is null)
            return LearningErrors.LectureTopicNotFound;

        if (!topic.IsActive)
            return LearningErrors.LectureTopicInactive;

        return await ValidateDifficultyAsync(dbContext, difficultyId, cancellationToken);
    }

    public static async Task<Error?> ValidateDifficultyAsync(
        LearningDbContext dbContext,
        string? difficultyId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(difficultyId))
            return LearningErrors.LectureDifficultyRequired;

        var difficulty = await dbContext.TagDifficulties
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.DifficultyId == difficultyId, cancellationToken);

        if (difficulty is null)
            return LearningErrors.LectureDifficultyNotFound;

        return difficulty.IsActive ? null : LearningErrors.LectureDifficultyInactive;
    }
}
