namespace MathInsight.Modules.TestGen.Persistence.ReadModels;

public sealed class QuestionVersionReadModel
{
    public string VersionId { get; set; } = string.Empty;
    public string QuestionId { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public short SnapshotSchemaVersion { get; set; }
    public string AnswersSnapshot { get; set; } = string.Empty;
    public DateTime CreatedTime { get; set; }
    public ICollection<Entities.TestQuestion> TestQuestions { get; set; } = new List<Entities.TestQuestion>();
}
