namespace MathInsight.Modules.QuestionBank.Entities;

public class QuestionVersion
{
    public string VersionId { get; set; } = default!;
    public string QuestionId { get; set; } = default!;
    public string QuestionContent { get; set; } = default!;
    public string QuestionAnswer { get; set; } = default!;
    public string AnswersSnapshot { get; set; } = default!;
    public string? PictureUrl { get; set; }
    public DateTime CreatedTime { get; set; }
    public string ExpertId { get; set; } = default!;

    public Question Question { get; set; } = default!;
}
