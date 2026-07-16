using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Grading_Analytics.Persistence;

namespace MathInsight.Modules.Grading_Analytics.Queries.GetSessionHistory;

/// <summary>
/// Handles GetSessionHistoryQuery (UC-56 — paginated history).
/// Returns only Graded sessions ordered by EndTime DESC (BR-UC56-01, BR-UC56-02).
/// </summary>
public sealed class GetSessionHistoryQueryHandler
    : IRequestHandler<GetSessionHistoryQuery, PagedResult<SessionHistoryDto>>
{
    private readonly GradingDbContext _db;

    public GetSessionHistoryQueryHandler(GradingDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<SessionHistoryDto>> Handle(
        GetSessionHistoryQuery request,
        CancellationToken cancellationToken)
    {
        // BR-UC56-03: clamp pageSize
        var pageSize = Math.Min(request.PageSize, 100);
        var page = Math.Max(request.Page, 1);

        var query = _db.TestSessions
            .AsNoTracking()
            .Where(s => s.StudentId == request.StudentId && s.Status == "Graded");

        // Optional filters
        if (!string.IsNullOrWhiteSpace(request.TestFormat))
            query = query.Where(s => s.TestFormat == request.TestFormat);

        if (request.FromDate.HasValue)
            query = query.Where(s => s.EndTime >= request.FromDate.Value);

        if (request.ToDate.HasValue)
            query = query.Where(s => s.EndTime <= request.ToDate.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .OrderByDescending(s => s.EndTime) // BR-UC56-02
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SessionHistoryDto
            {
                SessionId = s.SessionId,
                TestId = s.TestId,
                TestFormat = s.TestFormat,
                Status = s.Status,
                Score = s.Score,
                NumCorrect = s.NumCorrect,
                NumIncorrect = s.NumIncorrect,
                NumAbandoned = s.NumAbandoned,
                TotalQuestion = s.TotalQuestion,
                DurationMinutes = s.Duration,
                SubmittedAt = s.EndTime,
                SubmissionType = s.SubmissionType,
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<SessionHistoryDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Items = items,
        };
    }
}

/// <summary>
/// Handles GetStudentHistoryStatsQuery (UC-56 — aggregate stats).
/// </summary>
public sealed class GetStudentHistoryStatsQueryHandler
    : IRequestHandler<GetStudentHistoryStatsQuery, StudentHistoryStatsDto>
{
    private readonly GradingDbContext _db;

    public GetStudentHistoryStatsQueryHandler(GradingDbContext db)
    {
        _db = db;
    }

    public async Task<StudentHistoryStatsDto> Handle(
        GetStudentHistoryStatsQuery request,
        CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-30);

        var sessions = await _db.TestSessions
            .AsNoTracking()
            .Where(s => s.StudentId == request.StudentId && s.Status == "Graded")
            .Select(s => new
            {
                s.Score,
                s.NumCorrect,
                s.TotalQuestion,
                s.EndTime,
            })
            .ToListAsync(cancellationToken);

        if (sessions.Count == 0)
        {
            return new StudentHistoryStatsDto
            {
                TotalSessions = 0,
                SessionsLast30Days = 0,
                AverageScore = 0m,
                AccuracyPercent = 0m,
            };
        }

        var totalSessions = sessions.Count;
        var sessionsLast30Days = sessions.Count(s => s.EndTime >= cutoff);
        var averageScore = Math.Round(sessions.Average(s => s.Score), 2);

        var totalCorrect = sessions.Sum(s => s.NumCorrect);
        var totalQuestions = sessions.Sum(s => s.TotalQuestion);
        var accuracyPercent = totalQuestions > 0
            ? Math.Round(totalCorrect * 100.0m / totalQuestions, 2)
            : 0m;

        return new StudentHistoryStatsDto
        {
            TotalSessions = totalSessions,
            SessionsLast30Days = sessionsLast30Days,
            AverageScore = averageScore,
            AccuracyPercent = accuracyPercent,
        };
    }
}
