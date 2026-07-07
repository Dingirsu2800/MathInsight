namespace MathInsight.Modules.QuestionBank.Contracts.Questions;

public sealed record UpdateQuestionResponse(
    string QuestionId,
    string Status,
    bool VersionCreated);
