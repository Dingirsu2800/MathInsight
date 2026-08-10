namespace MathInsight.Modules.Recommender.Contracts;

/// <summary>
/// Full mastery snapshot for a topic returned by
/// <see cref="Services.IRecommenderService.GetStudentAllTagsMasteryAsync"/>.
/// Unlike <see cref="WeakTagDto"/>, this DTO covers ALL mastery statuses
/// (NotLearned, Learning, Mastered), not only weak topics.
/// </summary>
/// <param name="TagId">Topic tag identifier.</param>
/// <param name="TagName">Display name resolved from TagTopic.</param>
/// <param name="OfficialPoint">Current official score (0.00–10.00).</param>
/// <param name="NumberDone">Total graded sessions that affected this topic.</param>
/// <param name="MasteryStatus">Coarse label: NotLearned | Learning | Mastered.</param>
/// <param name="RecommendedDifficultyLevel">Next recommended practice level (1–4, see RCM-07).</param>
public sealed record TagMasteryDto(
    string TagId,
    string TagName,
    decimal OfficialPoint,
    int NumberDone,
    string MasteryStatus,
    byte RecommendedDifficultyLevel);
