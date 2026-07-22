namespace MathInsight.Modules.QuestionBank.Entities;

public class Question
{
    public string QuestionId { get; set; } = default!;
    public string QuestionContent { get; set; } = default!;
    public string SolutionContent { get; set; } = default!;
    public string? PictureUrl { get; set; }
    public string DifficultyId { get; set; } = default!;
    public int Grade { get; set; }
    public string Status { get; set; } = default!;
    public string QuestionType { get; set; } = default!;
    public string ExpertId { get; set; } = default!;
    public decimal DefaultWeight { get; set; } = 1.00m;
    public bool IsActive { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }

    public TagDifficulty Difficulty { get; set; } = default!;
    public ICollection<Answer> Answers { get; set; } = new List<Answer>();
    public ICollection<QuestionPart> Parts { get; set; } = new List<QuestionPart>();
    public ICollection<QuestionTopic> QuestionTopics { get; set; } = new List<QuestionTopic>();
    public ICollection<QuestionVersion> Versions { get; set; } = new List<QuestionVersion>();
    public ICollection<QuestionReport> Reports { get; set; } = new List<QuestionReport>();
}
