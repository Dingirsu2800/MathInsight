using MathInsight.Modules.Testing.Entities;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Testing.Persistence;

public class TestingDbContext : DbContext
{
    public TestingDbContext(DbContextOptions<TestingDbContext> options) : base(options) { }

    public DbSet<Blueprint> Blueprints => Set<Blueprint>();
    public DbSet<Test> Tests => Set<Test>();
    public DbSet<TestQuestion> TestQuestions => Set<TestQuestion>();
    public DbSet<QuestionVersion> QuestionVersions => Set<QuestionVersion>();
    public DbSet<TestSession> TestSessions => Set<TestSession>();
    public DbSet<TestAnswer> TestAnswers => Set<TestAnswer>();
    public DbSet<TestAnswerOption> TestAnswerOptions => Set<TestAnswerOption>();
    public DbSet<TestAnswerPart> TestAnswerParts => Set<TestAnswerPart>();
    public DbSet<TestIncident> TestIncidents => Set<TestIncident>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TestingDbContext).Assembly);
    }
}
