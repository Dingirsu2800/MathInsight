namespace MathInsight.Modules.QuestionBank.Contracts.Questions;

public sealed class CreateQuestionRequest
{
    public string QuestionContent { get; set; } = string.Empty;
    public string SolutionContent { get; set; } = string.Empty;
    public string? PictureUrl { get; set; }
    public string DifficultyId { get; set; } = string.Empty;
    public int Grade { get; set; }
    public string QuestionType { get; set; } = string.Empty;
    public decimal DefaultPoint { get; set; } = 0.20m;
    public List<CreateQuestionTopicRequest> Topics { get; set; } = [];
    public List<CreateAnswerRequest> Answers { get; set; } = [];
    public List<CreateQuestionPartRequest> Parts { get; set; } = [];
}
