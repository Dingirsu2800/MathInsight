using MediatR;

namespace MathInsight.Modules.Grading_Analytics.Queries.GetSessionHistory;

/// <summary>
/// UC-56: Paginated list of graded sessions for the authenticated student.
/// </summary>
public sealed record GetSessionHistoryQuery(
    Guid StudentId,
    int Page,
    int PageSize,
    string? TestFormat,
    DateTime? FromDate,
    DateTime? ToDate) : IRequest<PagedResult<SessionHistoryDto>>;

/// <summary>
/// UC-56: Aggregate statistics across all graded sessions for the authenticated student.
/// </summary>
public sealed record GetStudentHistoryStatsQuery(
    Guid StudentId) : IRequest<StudentHistoryStatsDto>;
