namespace MathInsight.Modules.TestGen.Contracts.Tests;

public sealed class GenerateSharedBlueprintExamRequest
{
    public string TestName { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
}

public sealed class GenerateFixedBlueprintExamRequest
{
    public string TestName { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public IReadOnlyList<FixedBlueprintExamQuestionRequest> Questions { get; set; } = [];
}

public sealed class FixedBlueprintExamQuestionRequest
{
    public string QuestionId { get; set; } = string.Empty;
    public string BlueprintDetailId { get; set; } = string.Empty;
    public int QuestionOrder { get; set; }
}

public sealed record FixedTestCandidateResponse(
    string QuestionId,
    string QuestionVersionId,
    string BlueprintDetailId,
    string TagId,
    string QuestionType,
    string DifficultyId,
    int PartCount,
    decimal DefaultWeight,
    IReadOnlyList<string> SupportedScoringRules,
    string QuestionContent,
    string? PictureUrl);

public sealed record PagedFixedTestCandidateResponse(
    int PageIndex,
    int PageSize,
    int TotalCount,
    IReadOnlyList<FixedTestCandidateResponse> Items);

public sealed record GeneratedTestQuestionResponse(
    string QuestionId,
    string QuestionVersionId,
    int QuestionOrder,
    string SourceBlueprintDetailId,
    decimal WeightSnapshot,
    decimal MaxPointsSnapshot,
    string ScoringRuleSnapshot);

public sealed record GenerateSharedBlueprintExamResponse(
    string TestId,
    string BlueprintId,
    string TestCode,
    string TestMode,
    string TestStatus,
    string GeneratedBy,
    string? GeneratedForStudentId,
    string TestName,
    int DurationMinutes,
    int TotalQuestions,
    decimal MaxScore,
    string ScoringPolicy,
    DateTime CreatedTime,
    IReadOnlyList<GeneratedTestQuestionResponse> Questions);

public sealed class UpdateGeneratedTestStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

public sealed record UpdateGeneratedTestStatusResponse(
    string TestId,
    string TestStatus);

public sealed class ResolveTestCodeRequest
{
    public string TestCode { get; set; } = string.Empty;
}

public sealed record SharedBlueprintExamResponse(
    string TestId,
    string BlueprintId,
    string TestName,
    string? TestCode,
    string GenerationType,
    int Grade,
    int DurationMinutes,
    int TotalQuestions,
    decimal MaxScore,
    DateTime CreatedTime);

public sealed record PagedSharedBlueprintExamResponse(
    int PageIndex,
    int PageSize,
    int TotalCount,
    IReadOnlyList<SharedBlueprintExamResponse> Items);

public sealed record ExpertGeneratedTestListItemResponse(
    string TestId,
    string BlueprintId,
    string TestName,
    string TestCode,
    string TestStatus,
    string GenerationType,
    int DurationMinutes,
    int TotalQuestions,
    decimal MaxScore,
    DateTime CreatedTime);

public sealed record PagedExpertGeneratedTestResponse(
    int PageIndex,
    int PageSize,
    int TotalCount,
    IReadOnlyList<ExpertGeneratedTestListItemResponse> Items);

public sealed record ExpertTestPreviewResponse(
    string TestId,
    string BlueprintId,
    string TestName,
    string TestCode,
    string TestStatus,
    string GenerationType,
    int DurationMinutes,
    int TotalQuestions,
    decimal MaxScore,
    DateTime CreatedTime,
    IReadOnlyList<ExpertTestPreviewSectionResponse> Sections);

public sealed record ExpertTestPreviewSectionResponse(
    string BlueprintSectionId,
    int SectionOrder,
    string? SectionCode,
    string SectionName,
    string QuestionType,
    string? InstructionText,
    decimal ScoreBudget,
    string ScoringRule,
    IReadOnlyList<ExpertTestPreviewQuestionResponse> Questions);

public sealed record ExpertTestPreviewQuestionResponse(
    string QuestionId,
    string QuestionVersionId,
    int QuestionOrder,
    string SourceBlueprintDetailId,
    string QuestionType,
    string? QuestionContent,
    string? SolutionContent,
    string? PictureUrl,
    decimal WeightSnapshot,
    decimal MaxPointsSnapshot,
    string ScoringRuleSnapshot,
    IReadOnlyList<ExpertPreviewAnswerResponse> Answers,
    IReadOnlyList<ExpertPreviewPartResponse> Parts);

public sealed record ExpertPreviewAnswerResponse(
    string AnswerId,
    string AnswerContent,
    bool IsCorrect);

public sealed record ExpertPreviewPartResponse(
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
