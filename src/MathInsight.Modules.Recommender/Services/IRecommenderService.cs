using MathInsight.Modules.Recommender.Contracts;

namespace MathInsight.Modules.Recommender.Services;

/// <summary>
/// In-process API for querying student weak tags and advisory data.
/// Used directly by Recommender queries and cross-module by TestGen (RCM-09).
/// </summary>
public interface IRecommenderService
{
    /// <summary>
    /// Returns topics where <c>official_point &lt; 5.00</c> for the given student.
    /// Topics with no <c>TagsMastery</c> row are NOT returned as weak (spec edge case: No-row behavior).
    /// </summary>
    Task<IReadOnlyList<WeakTagDto>> GetStudentWeakTagsAsync(string studentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns detailed weak-tag advice including recommended difficulty level,
    /// remedial flag, and reason. Used by TestGen to select questions.
    /// </summary>
    Task<IReadOnlyList<WeakTagAdviceDto>> GetStudentWeakTagAdviceAsync(string studentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// UC-55: Returns all eligible direct-child <c>TagsMastery</c> rows for the given student,
    /// regardless of score. Includes NotLearned, Learning, and Mastered topics.
    /// Used by the Competency page to show a complete picture of the student's mastery (RCM-17).
    /// </summary>
    Task<IReadOnlyList<TagMasteryDto>> GetStudentAllTagsMasteryAsync(string studentId, CancellationToken cancellationToken = default);
}
