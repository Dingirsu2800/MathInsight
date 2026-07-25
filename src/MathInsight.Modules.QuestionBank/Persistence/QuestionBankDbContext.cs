using MathInsight.Modules.QuestionBank.Entities;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Persistence;

public class QuestionBankDbContext : DbContext
{
    public QuestionBankDbContext(DbContextOptions<QuestionBankDbContext> options)
        : base(options)
    {
    }

    public DbSet<Answer> Answers => Set<Answer>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionPart> QuestionParts => Set<QuestionPart>();
    public DbSet<QuestionReport> QuestionReports => Set<QuestionReport>();
    public DbSet<QuestionTopic> QuestionTopics => Set<QuestionTopic>();
    public DbSet<QuestionVersion> QuestionVersions => Set<QuestionVersion>();
    public DbSet<TagDifficulty> TagDifficulties => Set<TagDifficulty>();
    public DbSet<TagTopic> TagTopics => Set<TagTopic>();
    public DbSet<AccountReadModel> AccountReadModels => Set<AccountReadModel>();
    public DbSet<TestQuestionReadModel> TestQuestionReadModels => Set<TestQuestionReadModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QuestionBankDbContext).Assembly);
    }
}
