namespace MathInsight.Modules.Gamification.Services;

/// <summary>
/// Badge auto-award seam (BR-43). Called after each recorded activity to check every not-yet-earned
/// badge's condition and insert a StudentBadge when met. The award check is idempotent — the
/// StudentBadge composite PK blocks duplicate awards.
///
/// Owned by Student B; only the interface and a temporary no-op live here so the streak flow runs
/// end-to-end now. B swaps in the real implementation by changing one DI line.
/// </summary>
public interface IBadgeService
{
    Task CheckAndAwardBadgesAsync(string studentId, CancellationToken cancellationToken = default);
}
