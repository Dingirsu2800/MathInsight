namespace MathInsight.Modules.QuestionBank.Contracts.Tags;

public sealed record TagTopicTreeResponse(
    string TagId,
    string? ParentTagId,
    string TagName,
    string? Description,
    int Grade,
    int DisplayOrder,
    bool IsActive,
    IReadOnlyList<TagTopicTreeResponse> Children
);
