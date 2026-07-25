namespace MathInsight.Modules.Learning_Lecture.Entities;

public class TagTopicReadOnly
{
    public string TagId { get; set; } = default!;
    public string TagName { get; set; } = default!;
    public string? ParentTagId { get; set; }
    public int Grade { get; set; }
}
