using MathInsight.Modules.Gamification.Entities;
using MathInsight.Modules.Gamification.Enums;
using MathInsight.Modules.Gamification.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Gamification.Services;

/// <summary>
/// Consecutive-day study streak logic (BR-39..BR-42, spec.md is the source of truth).
///
/// The streak is day-based: at most one qualifying activity per calendar day advances it.
/// A gap of more than one day resets the count — and because a NEW qualifying activity arrived
/// today, the reset lands on 1 (day one of a fresh streak), never 0. BR-41 describes 0 only as a
/// display state for an already-broken streak with no activity today.
/// </summary>
public class StreakService : IStreakService
{
    // BR-39: a lecture view counts only when it reaches five minutes.
    private const int MinLectureQualifyingSeconds = 300;

    private readonly GamificationDbContext _dbContext;

    public StreakService(GamificationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task UpdateStreakAsync(
        string studentId,
        ActivityType activityType,
        int durationSeconds,
        DateOnly activityDate,
        CancellationToken cancellationToken = default)
    {
        // BR-39: non-qualifying activity never touches the streak (no row, no update).
        if (!Qualifies(activityType, durationSeconds))
        {
            return;
        }

        // Tracked (not AsNoTracking) — this path updates the row.
        var streak = await _dbContext.StudyStreaks
            .FirstOrDefaultAsync(s => s.StudentId == studentId, cancellationToken);

        // First-ever qualifying activity for this student: day one.
        if (streak is null)
        {
            _dbContext.StudyStreaks.Add(new StudyStreak
            {
                StreakId = Guid.NewGuid().ToString(),
                StudentId = studentId,
                CurrentStreak = 1,
                LongestStreak = 1,
                LastActivityDate = activityDate
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        // Same calendar day: already counted today, do not re-count (idempotent).
        if (streak.LastActivityDate == activityDate)
        {
            return;
        }

        if (streak.LastActivityDate == activityDate.AddDays(-1))
        {
            // Yesterday → the run continues.
            streak.CurrentStreak += 1;
        }
        else
        {
            // Gap of more than one day (or no prior date): a fresh streak starts today (BR-41).
            streak.CurrentStreak = 1;
        }

        streak.LastActivityDate = activityDate;

        // BR-42: longest only ever grows. This also keeps CurrentStreak <= LongestStreak before
        // save, satisfying the DB CHECK constraint CK_StudyStreak_Values.
        if (streak.CurrentStreak > streak.LongestStreak)
        {
            streak.LongestStreak = streak.CurrentStreak;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // BR-39 qualification, centralised here so callers never reimplement it.
    private static bool Qualifies(ActivityType activityType, int durationSeconds) =>
        activityType switch
        {
            ActivityType.PRACTICE => true,
            ActivityType.EXAM => true,
            ActivityType.VIEW_LECTURE => durationSeconds >= MinLectureQualifyingSeconds,
            _ => false // DOWNLOAD_MATERIAL never qualifies.
        };
}
