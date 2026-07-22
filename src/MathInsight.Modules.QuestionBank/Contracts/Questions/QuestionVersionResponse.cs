namespace MathInsight.Modules.QuestionBank.Contracts.Questions;

public sealed record QuestionVersionResponse(
    string VersionId,
    string QuestionId,
    string QuestionContent,
    string QuestionAnswer,
    string AnswersSnapshot,
    string? PictureUrl,
    int VersionNumber,
    short SnapshotSchemaVersion,
    DateTime CreatedTime,
    string ExpertId,
    string? ExpertName,
    string Status);
