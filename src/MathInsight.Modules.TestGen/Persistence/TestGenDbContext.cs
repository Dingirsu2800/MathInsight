using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Modules.TestGen.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.TestGen.Persistence;

/// <summary>
/// Test Generator persistence boundary. Database scripts own the physical schema.
/// </summary>
public class TestGenDbContext : DbContext
{
    public TestGenDbContext(DbContextOptions<TestGenDbContext> options) : base(options) { }

    public DbSet<Blueprint> Blueprints => Set<Blueprint>();
    public DbSet<BlueprintSection> BlueprintSections => Set<BlueprintSection>();
    public DbSet<BlueprintDetail> BlueprintDetails => Set<BlueprintDetail>();
    public DbSet<Test> Tests => Set<Test>();
    public DbSet<TestQuestion> TestQuestions => Set<TestQuestion>();
    public DbSet<AccountReadModel> Accounts => Set<AccountReadModel>();
    public DbSet<ExpertReadModel> Experts => Set<ExpertReadModel>();
    public DbSet<TagTopicReadModel> TagTopics => Set<TagTopicReadModel>();
    public DbSet<TagDifficultyReadModel> TagDifficulties => Set<TagDifficultyReadModel>();
    public DbSet<StudentReadModel> Students => Set<StudentReadModel>();
    public DbSet<QuestionReadModel> Questions => Set<QuestionReadModel>();
    public DbSet<QuestionTopicReadModel> QuestionTopics => Set<QuestionTopicReadModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TestGenDbContext).Assembly);
    }
}
