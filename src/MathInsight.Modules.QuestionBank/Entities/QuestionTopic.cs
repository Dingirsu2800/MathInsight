namespace MathInsight.Modules.QuestionBank.Entities;

public class QuestionTopic
{
    public string QuestionTopicId { get; set; } = default!;
    public string QuestionId { get; set; } = default!;
    public string TagId { get; set; } = default!;
    public bool IsPrimary { get; set; }

    public Question Question { get; set; } = default!;
    public TagTopic Tag { get; set; } = default!;
}
