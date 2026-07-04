using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Recommender.Persistence.Entities;
using MathInsight.Modules.Recommender.Persistence.Configurations;

namespace MathInsight.Modules.Recommender.Persistence;

/// <summary>
/// Recommender module DbContext.
/// This module OWNS: CompetencyPoint, TagsMastery, StudentTopicSessionResult.
/// Uses the same shared SQL Server connection string as other modules.
/// No EF migrations — table structure is managed by DB scripts (source of truth).
/// </summary>
public class RecommenderDbContext : DbContext
{
    public RecommenderDbContext(DbContextOptions<RecommenderDbContext> options) : base(options) { }

    // Recommender-owned tables
    public DbSet<CompetencyPoint> CompetencyPoints => Set<CompetencyPoint>();
    public DbSet<TagsMastery> TagsMasteries => Set<TagsMastery>();
    public DbSet<StudentTopicSessionResult> StudentTopicSessionResults => Set<StudentTopicSessionResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all EF configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RecommenderDbContext).Assembly);
    }
}
