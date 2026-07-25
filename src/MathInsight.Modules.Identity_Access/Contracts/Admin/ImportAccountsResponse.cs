namespace MathInsight.Modules.Identity_Access.Contracts.Admin;

public sealed record ImportAccountsResponse(
    int TotalRows,
    int SuccessCount,
    int SkippedCount,
    IReadOnlyList<ImportRowResult> SkippedRows);

public sealed record ImportRowResult(
    int RowNumber,
    string? Username,
    string? Email,
    string Reason);
