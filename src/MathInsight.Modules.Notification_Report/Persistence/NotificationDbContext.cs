using MathInsight.Modules.Notification_Report.Entities;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Notification_Report.Persistence;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Notification> Notifications => Set<Notification>();

    // Cross-module read-only tables (not owned by this module) — used by the Leaderboard query.
    public DbSet<CompetencyPointReadOnly> CompetencyPoints => Set<CompetencyPointReadOnly>();
    public DbSet<AccountReadOnly> Accounts => Set<AccountReadOnly>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationDbContext).Assembly);
    }
}
