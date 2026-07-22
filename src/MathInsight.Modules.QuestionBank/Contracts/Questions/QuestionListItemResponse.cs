namespace MathInsight.Modules.QuestionBank.Contracts.Questions;

public sealed record QuestionListItemResponse(
    string QuestionId,
    string QuestionContent,
    string? PictureUrl,
    string DifficultyId,
    string DifficultyName,
    int DifficultyLevel,
    int Grade,
    string Status,
    string QuestionType,
    string ExpertId,
    string? ExpertName,
    decimal DefaultWeight,
    bool IsActive,
    DateTime CreatedTime,
    DateTime UpdatedTime,
    IReadOnlyList<QuestionTopicSummaryResponse> Topics);
