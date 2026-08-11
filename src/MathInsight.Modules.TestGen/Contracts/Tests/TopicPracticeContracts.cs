namespace MathInsight.Modules.TestGen.Contracts.Tests;

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
    string? RecommendationReason);
public sealed record TopicPracticeOptionsResponse(int Grade, int RequiredQuestionCount, IReadOnlyList<TopicPracticeTopicResponse> Topics);
