using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;
using MathInsight.Modules.Grading_Analytics.Persistence.Configurations;

namespace MathInsight.Modules.Grading_Analytics.Persistence;

/// <summary>
/// Grading module DbContext.
/// This module does NOT own any tables. It cross-reads tables owned by Testing (003)
/// and QuestionBank (002) using the same shared SQL Server connection string.
/// No EF migrations should be added here. Table structure is managed by SQL scripts.
/// </summary>
public class GradingDbContext : DbContext
{
    public GradingDbContext(DbContextOptions<GradingDbContext> options) : base(options) { }

    // Cross-read from Testing module (003) — Grading writes grading result fields only
    public DbSet<TestSession> TestSessions => Set<TestSession>();
    public DbSet<TestAnswer> TestAnswers => Set<TestAnswer>();
    public DbSet<TestAnswerOption> TestAnswerOptions => Set<TestAnswerOption>();
    public DbSet<TestAnswerPart> TestAnswerParts => Set<TestAnswerPart>();

    // Cross-read from QuestionBank module (002) — read-only
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Answer> Answers => Set<Answer>();
    public DbSet<QuestionPart> QuestionParts => Set<QuestionPart>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all EF configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GradingDbContext).Assembly);
    }
}
