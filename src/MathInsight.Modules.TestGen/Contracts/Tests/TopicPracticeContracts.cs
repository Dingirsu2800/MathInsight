namespace MathInsight.Modules.TestGen.Contracts.Tests;

public sealed record TopicPracticeDifficultyAvailabilityResponse(
    string DifficultyId,
    string DifficultyName,
    byte LevelValue,
    int AvailableQuestionCount,
    bool CanGenerate);

public sealed record TopicPracticeTopicResponse(
    string TagId,
    string? ParentTagId,
    string ParentTagName,
    string TagName,
    int Grade,
    int DisplayOrder,
    int AvailableQuestionCount,
    bool CanGenerate,
    bool IsWeakRecommended,
    string? WeakTagId,
    string? WeakTagName,
    decimal? OfficialPoint,
    int? EvidenceCount,
    byte? RecommendedDifficultyLevel,
    string? RecommendationReason,
    IReadOnlyList<TopicPracticeDifficultyAvailabilityResponse> DifficultyAvailability);
public sealed record TopicPracticeOptionsResponse(int Grade, int RequiredQuestionCount, IReadOnlyList<TopicPracticeTopicResponse> Topics);
