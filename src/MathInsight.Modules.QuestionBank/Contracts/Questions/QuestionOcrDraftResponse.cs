namespace MathInsight.Modules.QuestionBank.Contracts.Questions;

public sealed record QuestionOcrDraftResponse(
    string RawMarkdown,
    decimal? PageConfidence,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<QuestionOcrExtractedImage> ExtractedImages,
    QuestionOcrDraft Draft);

public sealed record QuestionOcrExtractedImage(
    string Id,
    string DataUrl,
    string? Annotation);

public sealed record QuestionOcrDraft(
    string QuestionContent,
    string SolutionContent,
    string SuggestedQuestionType,
    IReadOnlyList<QuestionOcrAnswerDraft> Answers,
    IReadOnlyList<QuestionOcrPartDraft> Parts);

public sealed record QuestionOcrAnswerDraft(
    string Content,
    bool? SuggestedIsCorrect);

public sealed record QuestionOcrPartDraft(
    string? Label,
    string Content,
    string PartType,
    string? Explanation,
    bool? SuggestedCorrectBoolean,
    string? SuggestedCorrectText,
    decimal? SuggestedCorrectNumeric,
    decimal? NumericTolerance);
