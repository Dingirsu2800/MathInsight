namespace MathInsight.Modules.QuestionBank.Contracts.Questions;

public sealed record DeleteQuestionResponse(
    string QuestionId,
    string DeleteMode,
    bool IsActive,
    string Status);
