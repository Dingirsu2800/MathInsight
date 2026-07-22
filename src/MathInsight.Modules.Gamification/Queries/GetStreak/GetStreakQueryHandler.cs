using MathInsight.Modules.Gamification.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Gamification.Queries.GetStreak;

/// <summary>
/// UC-81. Read-only. Loads the student's StudyStreak row and applies the display rule.
/// </summary>
public class GetStreakQueryHandler : IRequestHandler<GetStreakQuery, Result<StreakResponse>>
{
    private readonly GamificationDbContext _dbContext;

    public GetStreakQueryHandler(GamificationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<StreakResponse>> Handle(GetStreakQuery request, CancellationToken cancellationToken)
    {
        var streak = await _dbContext.StudyStreaks
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.StudentId == request.StudentId, cancellationToken);

        // A student who never had a qualifying activity simply has no row — a zero streak, not an
        // error. Return Success so the UI renders "0 / not active" rather than a failure.
        if (streak is null)
        {
            return Result<StreakResponse>.Success(new StreakResponse(0, 0, null, false));
        }

        // UC-81 display rule. The stored CurrentStreak is a persisted counter; whether it is still
        // "alive" today is a DISPLAY decision computed here and never written back (this is a read).
        // Active if the last qualifying activity was today or yesterday. A gap of more than one day
        // means the streak has lapsed: we surface CurrentStreak = 0 — exactly the spec's "reset to 0"
        // display semantics — WITHOUT mutating the row. The real reset-to-1/continue happens later,
        // when StreakService processes the next qualifying activity.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var isActive = streak.LastActivityDate == today
                       || streak.LastActivityDate == today.AddDays(-1);

        return Result<StreakResponse>.Success(new StreakResponse(
            CurrentStreak: isActive ? streak.CurrentStreak : 0,
            LongestStreak: streak.LongestStreak,
            LastActivityDate: streak.LastActivityDate,
            IsActive: isActive));
    }
}
