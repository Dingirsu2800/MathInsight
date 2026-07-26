using MathInsight.Modules.Notification_Report.Contracts;
using MathInsight.Modules.Notification_Report.Persistence;
using MathInsight.Modules.Notification_Report.Services;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Notification_Report.Jobs;

/// <summary>BR-19: recalculates and caches the ranking for every grade (10, 11, 12).</summary>
public class LeaderboardRecalculationJob
{
    private static readonly int[] Grades = { 10, 11, 12 };
    private const int Top = 50;

    private readonly NotificationDbContext _dbContext;
    private readonly ILeaderboardCacheService _cache;

    public LeaderboardRecalculationJob(NotificationDbContext dbContext, ILeaderboardCacheService cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var totalCached = 0;

        foreach (var grade in Grades)
        {
            var entries = await BuildLeaderboardAsync(grade, cancellationToken);
            await _cache.SetAsync(grade, entries, cancellationToken);
            totalCached += entries.Count;
        }

        return totalCached;
    }

    private async Task<IReadOnlyList<LeaderboardEntryDto>> BuildLeaderboardAsync(int grade, CancellationToken cancellationToken)
    {
        var ranked =
            from point in _dbContext.CompetencyPoints.AsNoTracking()
            join account in _dbContext.Accounts.AsNoTracking()
                on point.StudentId equals account.AccountId
            where point.Grade == grade
            orderby point.Point descending
            select new { point.StudentId, account.FirstName, account.LastName, point.Grade, point.Point };

        var rows = await ranked.Take(Top).ToListAsync(cancellationToken);

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
