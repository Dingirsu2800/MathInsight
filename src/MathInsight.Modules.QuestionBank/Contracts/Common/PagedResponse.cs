namespace MathInsight.Modules.QuestionBank.Contracts.Common;

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int PageIndex,
    int PageSize,
    int TotalCount,
    int TotalPages);
