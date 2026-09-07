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
    decimal DefaultWeight,
    bool IsActive,
    DateTime CreatedTime,
    DateTime UpdatedTime,
    IReadOnlyList<QuestionTopicResponse> Topics,
    IReadOnlyList<QuestionAnswerResponse> Answers,
    IReadOnlyList<QuestionPartResponse> Parts)
{
    public Contracts.Reports.ReportEligibilityResponse ReportEligibility { get; init; } = new(
        false, "AUTH_REQUIRED", null, null, null, null, null);
}
