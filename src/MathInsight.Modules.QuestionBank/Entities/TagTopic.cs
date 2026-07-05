namespace MathInsight.Modules.QuestionBank.Entities;

public class TagTopic
{
    public string TagId { get; set; } = default!;
    public string? ParentTagId { get; set; }
    public string TagName { get; set; } = default!;
    public string? Description { get; set; }
    public int Grade { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }

    public TagTopic? ParentTag { get; set; }
    public ICollection<TagTopic> ChildTags { get; set; } = new List<TagTopic>();
    public ICollection<QuestionTopic> QuestionTopics { get; set; } = new List<QuestionTopic>();

}
