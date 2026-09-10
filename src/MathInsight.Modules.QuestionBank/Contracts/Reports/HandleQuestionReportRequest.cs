namespace MathInsight.Modules.QuestionBank.Contracts.Reports;

public sealed class HandleQuestionReportRequest
{
    public string? Status { get; init; }
    public string? ResolutionAction { get; init; }
    public string? ReviewNote { get; init; }
}
