namespace MathInsight.Modules.Gamification.Services;

/// <summary>
/// Temporary no-op badge service so the module compiles and the streak flow works end-to-end
/// before Student B's real badge logic exists.
/// TODO: replaced by Student B's BadgeService (BR-43) — swap the DI registration in
/// GamificationModuleExtensions, no handler change required.
/// </summary>
public sealed class NullBadgeService : IBadgeService
{
    public Task CheckAndAwardBadgesAsync(string studentId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
