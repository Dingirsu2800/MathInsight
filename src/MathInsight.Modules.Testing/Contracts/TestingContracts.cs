namespace MathInsight.Modules.Testing.Contracts;

// ─── StartSession ───

public sealed record StartSessionRequest(string TestId);

public sealed record StartSessionResponse(
    string SessionId,
    string TestId,
    string TestFormat,
    string Status,
    DateTime StartTime,
    int DurationMinutes,
    int TotalQuestions,
    IReadOnlyList<SessionQuestionDto> Questions);

public sealed record SessionQuestionDto(
    string QuestionId,
    int QuestionOrder);

public sealed record TestSessionViewResponse(
    string SessionId,
    string TestId,
    string TestName,
    string Status,
    int DurationMinutes,
    decimal MaxScore,
    IReadOnlyList<StudentQuestionResponse> Questions);

public sealed record StudentQuestionResponse(
    string QuestionId,
    string QuestionVersionId,
    int QuestionNo,
    string QuestionType,
    string QuestionContent,
    string? PictureUrl,
    decimal MaxPoints,
    IReadOnlyList<StudentAnswerOptionResponse> AnswerOptions,
    IReadOnlyList<StudentQuestionPartResponse> Parts);

public sealed record StudentAnswerOptionResponse(string AnswerId, string AnswerContent);

public sealed record StudentQuestionPartResponse(
    string PartId,
    int PartOrder,
    string? PartLabel,
    string PartContent,
    string PartType);

// ─── AutoSave ───

public sealed record AutoSaveRequest(
    IReadOnlyList<AutoSaveAnswerDto> Answers);

public sealed record AutoSaveAnswerDto(
    string QuestionId,
    string? AnswerId,
    string? ShortAnswerText,
    int? TimeSpent,
    IReadOnlyList<AutoSaveOptionDto>? SelectedOptions,
    IReadOnlyList<AutoSavePartDto>? Parts);

public sealed record AutoSaveOptionDto(string AnswerId);

public sealed record AutoSavePartDto(
    string PartId,
    bool? BooleanAnswer,
    string? TextAnswer,
    decimal? NumericAnswer);

public sealed record AutoSaveResponse(
    DateTime SavedAt,
    int RemainingSeconds);

// ─── RecordIncident ───

public sealed record RecordIncidentRequest(string Type);

public sealed record RecordIncidentResponse(
    string IncidentId,
    int TotalIncidents,
    bool ForceSubmitted);

// ─── SubmitSession ───

public sealed record SubmitSessionResponse(
    string SessionId,
    string Status,
    string SubmissionType,
    int NumAbandoned,
    decimal? Score);

// ─── ForceSubmitSession (internal, no request DTO needed from client) ───

// ─── ReportSessionQuestion ───

public sealed record ReportSessionQuestionRequest(string Reason);

// ─── GetDetailedSolution ───

public sealed record DetailedSolutionResponse(
    string SessionId,
    string TestName,
    decimal Score,
    int NumCorrect,
    int NumIncorrect,
    int NumAbandoned,
    IReadOnlyList<SolutionQuestionDto> Questions);

public sealed record SolutionQuestionDto(
    string QuestionId,
    int QuestionNo,
    bool? IsCorrect,
    decimal PointsEarned,
    string? ShortAnswerText,
    string? SelectedAnswerId,
    IReadOnlyList<SolutionOptionDto> SelectedOptions,
    IReadOnlyList<SolutionPartDto> Parts);

public sealed record SolutionOptionDto(string AnswerId);

public sealed record SolutionPartDto(
    string PartId,
    bool? BooleanAnswer,
    string? TextAnswer,
    decimal? NumericAnswer,
    bool? IsCorrect,
    decimal PointsEarned);
