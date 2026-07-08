namespace MathInsight.Modules.QuestionBank.Contracts.Questions;

public sealed record QuestionTopicResponse(
    string TagId,
    string TagName,
    bool IsPrimary);
