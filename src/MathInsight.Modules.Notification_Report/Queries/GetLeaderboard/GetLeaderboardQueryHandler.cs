using MathInsight.Modules.Notification_Report.Contracts;
using MathInsight.Modules.Notification_Report.Errors;
using MathInsight.Modules.Notification_Report.Persistence;
using MathInsight.Modules.Notification_Report.Services;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Notification_Report.Queries.GetLeaderboard;

/// <summary>
/// UC-58. Reads the cached daily ranking first (BR-19); on a cache miss (or when Redis is
/// disabled — see NullLeaderboardCacheService), falls back to a live join of CompetencyPoint +
/// Account for the requested grade, and writes the result back to cache so the next read is fast
/// even if the recalculation job hasn't run yet.
/// </summary>
public sealed class GetLeaderboardQueryHandler
    : IRequestHandler<GetLeaderboardQuery, Result<IReadOnlyList<LeaderboardEntryDto>>>
{
    private static readonly int[] ValidGrades = { 10, 11, 12 };

    private readonly NotificationDbContext _dbContext;
    private readonly ILeaderboardCacheService _cache;

    public GetLeaderboardQueryHandler(NotificationDbContext dbContext, ILeaderboardCacheService cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<Result<IReadOnlyList<LeaderboardEntryDto>>> Handle(
        GetLeaderboardQuery request,
        CancellationToken cancellationToken)
    {
        if (!ValidGrades.Contains(request.Grade))
        {
            return Result<IReadOnlyList<LeaderboardEntryDto>>.Failure(NotificationErrors.LeaderboardGradeInvalid);
        }

        var cached = await _cache.GetAsync(request.Grade, cancellationToken);
        if (cached is not null)
        {
            return Result<IReadOnlyList<LeaderboardEntryDto>>.Success(cached);
        }

        var entries = await BuildLeaderboardAsync(request.Grade, request.Top, cancellationToken);

        await _cache.SetAsync(request.Grade, entries, cancellationToken);

        return Result<IReadOnlyList<LeaderboardEntryDto>>.Success(entries);
    }

    private async Task<IReadOnlyList<LeaderboardEntryDto>> BuildLeaderboardAsync(
        int grade, int top, CancellationToken cancellationToken)
    {
        var ranked =
            from point in _dbContext.CompetencyPoints.AsNoTracking()
            join account in _dbContext.Accounts.AsNoTracking()
                on point.StudentId equals account.AccountId
            where point.Grade == grade
            orderby point.Point descending
            select new { point.StudentId, account.FirstName, account.LastName, point.Grade, point.Point };

        var rows = await ranked.Take(top).ToListAsync(cancellationToken);

        return rows
            .Select((row, index) => new LeaderboardEntryDto(
                index + 1,
                row.StudentId,
                $"{row.FirstName} {row.LastName}".Trim(),
                row.Grade,
                row.Point))
            .ToList();
    }
}
