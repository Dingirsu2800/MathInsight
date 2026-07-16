namespace MathInsight.Modules.TestGen.Persistence.ReadModels;

public class TagTopicReadModel
{
    public string TagId { get; set; } = string.Empty;
    public string? ParentTagId { get; set; }
    public string TagName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Grade { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
}
