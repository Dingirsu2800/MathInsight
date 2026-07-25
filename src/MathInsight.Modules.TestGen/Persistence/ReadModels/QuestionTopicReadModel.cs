namespace MathInsight.Modules.TestGen.Persistence.ReadModels;

public sealed class QuestionTopicReadModel
{
    public string QuestionTopicId { get; set; } = string.Empty;
    public string QuestionId { get; set; } = string.Empty;
    public string TagId { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }

    public QuestionReadModel? Question { get; set; }
}
