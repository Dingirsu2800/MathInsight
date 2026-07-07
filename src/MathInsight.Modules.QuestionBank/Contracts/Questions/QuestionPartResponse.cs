namespace MathInsight.Modules.QuestionBank.Contracts.Questions;

public sealed record QuestionPartResponse(
    string PartId,
    int PartOrder,
    string? PartLabel,
    string PartContent,
    string PartType,
    bool? CorrectBoolean,
    string? CorrectText,
    decimal? CorrectNumeric,
    decimal? NumericTolerance,
    string? Explanation,
    decimal DefaultPoint);
