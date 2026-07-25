namespace MathInsight.Modules.QuestionBank.Contracts.Questions;

public sealed record ToggleQuestionActiveResponse(
    string QuestionId,
    bool IsActive,
    string Status);
