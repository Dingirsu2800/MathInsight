namespace MathInsight.Modules.QuestionBank.Contracts.Tags;

public sealed class CreateTagTopicRequest
{
    public string? ParentTagId { get; set; }
    public string TagName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Grade { get; set; }
    public int DisplayOrder { get; set; } = 1;
}
