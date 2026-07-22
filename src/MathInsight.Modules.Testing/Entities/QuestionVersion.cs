namespace MathInsight.Modules.Testing.Entities;

public sealed class QuestionVersion
{
    public string VersionId { get; set; } = string.Empty;
    public string QuestionId { get; set; } = string.Empty;
    public string QuestionContent { get; set; } = string.Empty;
    public string QuestionAnswer { get; set; } = string.Empty;
    public string AnswersSnapshot { get; set; } = string.Empty;
    public string? PictureUrl { get; set; }
    public short SnapshotSchemaVersion { get; set; }
}
