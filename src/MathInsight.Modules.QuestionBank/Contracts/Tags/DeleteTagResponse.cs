namespace MathInsight.Modules.QuestionBank.Contracts.Tags;

public sealed record DeleteTagResponse(
    string TagId,
    string DeleteMode,
    bool IsActive);
