namespace MathInsight.Shared.Questions;

public sealed record QuestionSnapshotV2(
    string QuestionId,
    string QuestionType,
    string DifficultyId,
    int Grade,
    decimal DefaultWeight,
    IReadOnlyList<QuestionTopicSnapshot> Topics,
    IReadOnlyList<QuestionAnswerSnapshot> Answers,
    IReadOnlyList<QuestionPartSnapshot> Parts,
    string? QuestionContent = null,
    string? SolutionContent = null,
    string? PictureUrl = null);

public sealed record QuestionTopicSnapshot(string TagId, bool IsPrimary);

public sealed record QuestionAnswerSnapshot(
    string AnswerId,
    string AnswerContent,
    bool IsCorrect);

public sealed record QuestionPartSnapshot(
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
    decimal DefaultWeight);
