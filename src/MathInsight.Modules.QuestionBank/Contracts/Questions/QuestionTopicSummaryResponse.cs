namespace MathInsight.Modules.QuestionBank.Contracts.Questions;

public sealed record QuestionTopicSummaryResponse(
    string TagId,
    string TagName,
    bool IsPrimary);
