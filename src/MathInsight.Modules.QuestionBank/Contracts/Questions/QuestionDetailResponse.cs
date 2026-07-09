namespace MathInsight.Modules.QuestionBank.Contracts.Questions;

public sealed record QuestionDetailResponse(
    string QuestionId,
    string QuestionContent,
    string SolutionContent,
    string? PictureUrl,
    string DifficultyId,
    string DifficultyName,
    int DifficultyLevel,
    int Grade,
    string Status,
    string QuestionType,
    string ExpertId,
    string? ExpertName,
    decimal DefaultPoint,
    bool IsActive,
    IReadOnlyList<QuestionTopicResponse> Topics,
    IReadOnlyList<QuestionAnswerResponse> Answers,
    IReadOnlyList<QuestionPartResponse> Parts);
