namespace MathInsight.Modules.Recommender.Contracts;

/// <summary>
/// Lightweight DTO returned by <see cref="Services.IRecommenderService.GetStudentWeakTagsAsync"/>.
/// Represents a topic where the student's OfficialPoint is below the weak threshold (&lt; 5.00).
/// </summary>
public sealed record WeakTagDto(
    Guid TagId,
    string TagName,
    decimal OfficialPoint);
