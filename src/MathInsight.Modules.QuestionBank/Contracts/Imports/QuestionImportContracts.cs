using MathInsight.Modules.QuestionBank.Contracts.Questions;

namespace MathInsight.Modules.QuestionBank.Contracts.Imports;

public sealed record QuestionImportIssueResponse(
    string Code,
    string Message,
    string Sheet,
    int? Row,
    string? Column,
    string? QuestionKey);

public sealed record QuestionImportPreviewItemResponse(
    string QuestionKey,
    int SourceRow,
    bool IsValid,
    IReadOnlyList<QuestionImportIssueResponse> Errors,
    CreateQuestionRequest? Draft);

public sealed record QuestionImportPreviewResponse(
    string ImportId,
    string FileName,
    int TotalCount,
    int ValidCount,
    int InvalidCount,
    IReadOnlyList<QuestionImportIssueResponse> FileErrors,
    IReadOnlyList<QuestionImportPreviewItemResponse> Items);

public sealed class ConfirmQuestionImportRequest
{
    public string ImportId { get; set; } = string.Empty;
    public List<ConfirmQuestionImportItemRequest> Items { get; set; } = [];
}

public sealed class ConfirmQuestionImportItemRequest
{
    public string QuestionKey { get; set; } = string.Empty;
    public CreateQuestionRequest? Draft { get; set; }
}

public sealed record ImportedQuestionResponse(string QuestionKey, string QuestionId);

public sealed record QuestionImportConfirmResponse(
    string Code,
    string ImportId,
    int CreatedCount,
    IReadOnlyList<ImportedQuestionResponse> Questions,
    IReadOnlyList<QuestionImportIssueResponse> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed record QuestionImportTemplateResponse(byte[] Content, string FileName, string ContentType);
