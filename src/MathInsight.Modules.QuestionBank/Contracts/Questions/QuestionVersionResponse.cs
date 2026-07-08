namespace MathInsight.Modules.QuestionBank.Contracts.Questions;

public sealed record QuestionVersionResponse(
    string VersionId,
    string QuestionId,
    string QuestionContent,
    string QuestionAnswer,
    string AnswersSnapshot,
    string? PictureUrl,
    DateTime CreatedTime,
    string ExpertId);
