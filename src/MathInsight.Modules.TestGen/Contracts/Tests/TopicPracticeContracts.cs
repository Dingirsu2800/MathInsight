namespace MathInsight.Modules.TestGen.Contracts.Tests;

public sealed record TopicPracticeTopicResponse(string TagId, string? ParentTagId, string TagName, int DisplayOrder, int AvailableQuestionCount, bool CanGenerate);
public sealed record TopicPracticeOptionsResponse(int Grade, int RequiredQuestionCount, IReadOnlyList<TopicPracticeTopicResponse> Topics);
