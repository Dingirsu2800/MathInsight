namespace MathInsight.Modules.TestGen.Persistence.ReadModels;

public sealed class QuestionReadModel
{
    public string QuestionId { get; set; } = string.Empty;
    public string DifficultyId { get; set; } = string.Empty;
    public int Grade { get; set; }
    public string Status { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    public ICollection<QuestionTopicReadModel> Topics { get; set; } = new List<QuestionTopicReadModel>();
    public ICollection<Entities.TestQuestion> TestQuestions { get; set; } = new List<Entities.TestQuestion>();
}
