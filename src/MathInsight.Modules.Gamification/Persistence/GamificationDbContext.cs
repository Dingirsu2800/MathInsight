using MathInsight.Modules.Gamification.Entities;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Gamification.Persistence;

public class GamificationDbContext : DbContext
{
    public GamificationDbContext(DbContextOptions<GamificationDbContext> options)
        : base(options)
    {
    }

    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<StudyStreak> StudyStreaks => Set<StudyStreak>();
    public DbSet<Badge> Badges => Set<Badge>();
    public DbSet<StudentBadge> StudentBadges => Set<StudentBadge>();
    public DbSet<TargetScore> TargetScores => Set<TargetScore>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GamificationDbContext).Assembly);
    }
}
