namespace MathInsight.Modules.QuestionBank.Contracts.Questions;

public sealed record QuestionAnswerResponse(
    string AnswerId,
    string AnswerContent,
    bool IsCorrect);
