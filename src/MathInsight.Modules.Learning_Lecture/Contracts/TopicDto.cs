using System.Collections.Generic;

namespace MathInsight.Modules.Learning_Lecture.Contracts;

public class TopicDto
{
    public string TagId { get; set; } = default!;
    public string TagName { get; set; } = default!;
    public string? ParentTagId { get; set; }
    public int Grade { get; set; }
    public List<TopicDto> Children { get; set; } = new();
}
