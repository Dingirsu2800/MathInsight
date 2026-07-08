using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.TestGen.Persistence.Entities;

namespace MathInsight.Modules.TestGen.Persistence;

/// <summary>
/// Test Generator module DbContext.
/// Maps to current DB script table names.
/// No EF migrations — table structure is managed by DB scripts.
/// </summary>
public class TestGenDbContext : DbContext
{
    public TestGenDbContext(DbContextOptions<TestGenDbContext> options) : base(options) { }

    public DbSet<Blueprint> Blueprints => Set<Blueprint>();
    public DbSet<BlueprintSection> BlueprintSections => Set<BlueprintSection>();
    public DbSet<BlueprintDetail> BlueprintDetails => Set<BlueprintDetail>();
    public DbSet<Test> Tests => Set<Test>();
    public DbSet<TestQuestion> TestQuestions => Set<TestQuestion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all EF configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TestGenDbContext).Assembly);
    }
}
