using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Testing.Persistence;

public sealed class TestingDbContext : DbContext
{
    public TestingDbContext(DbContextOptions<TestingDbContext> options) : base(options) { }

    public DbSet<TestReadModel> Tests => Set<TestReadModel>();
    public DbSet<TestQuestionReadModel> TestQuestions => Set<TestQuestionReadModel>();
    public DbSet<QuestionVersionReadModel> QuestionVersions => Set<QuestionVersionReadModel>();
    public DbSet<TestSession> TestSessions => Set<TestSession>();
    public DbSet<TestAnswer> TestAnswers => Set<TestAnswer>();
    public DbSet<TestAnswerOption> TestAnswerOptions => Set<TestAnswerOption>();
    public DbSet<TestAnswerPart> TestAnswerParts => Set<TestAnswerPart>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureTest(modelBuilder);
        ConfigureTestQuestion(modelBuilder);
        ConfigureQuestionVersion(modelBuilder);
        ConfigureSession(modelBuilder);
        ConfigureAnswers(modelBuilder);
    }

    private static void ConfigureTest(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<TestReadModel>();
        builder.ToTable("Test", table => table.ExcludeFromMigrations());
        builder.HasKey(item => item.TestId);
        builder.Property(item => item.TestId).HasColumnName("TestID").HasMaxLength(36).IsUnicode(false);
        builder.Property(item => item.TestName).HasColumnName("TestName");
        builder.Property(item => item.TestMode).HasColumnName("TestMode").HasMaxLength(30).IsUnicode(false);
        builder.Property(item => item.TestStatus).HasColumnName("TestStatus").HasMaxLength(20).IsUnicode(false);
        builder.Property(item => item.DurationMinutes).HasColumnName("DurationMinutes");
        builder.Property(item => item.TotalQuestions).HasColumnName("TotalQuestions");
        builder.Property(item => item.MaxScore).HasColumnName("MaxScore").HasPrecision(5, 2);
    }

    private static void ConfigureTestQuestion(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<TestQuestionReadModel>();
        builder.ToTable("TestQuestion", table => table.ExcludeFromMigrations());
        builder.HasKey(item => new { item.TestId, item.QuestionId });
        builder.Property(item => item.TestId).HasColumnName("TestID").HasMaxLength(36).IsUnicode(false);
        builder.Property(item => item.QuestionId).HasColumnName("QuestionID").HasMaxLength(36).IsUnicode(false);
        builder.Property(item => item.QuestionOrder).HasColumnName("QuestionOrder");
        builder.Property(item => item.QuestionVersionId).HasColumnName("QuestionVersionID").HasMaxLength(36).IsUnicode(false);
        builder.Property(item => item.MaxPointsSnapshot).HasColumnName("MaxPointsSnapshot").HasPrecision(5, 2);
        builder.Property(item => item.ScoringRuleSnapshot).HasColumnName("ScoringRuleSnapshot").HasMaxLength(30).IsUnicode(false);
    }

    private static void ConfigureQuestionVersion(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<QuestionVersionReadModel>();
        builder.ToTable("QuestionVersion", table => table.ExcludeFromMigrations());
        builder.HasKey(item => item.VersionId);
        builder.Property(item => item.VersionId).HasColumnName("VersionID").HasMaxLength(36).IsUnicode(false);
        builder.Property(item => item.QuestionId).HasColumnName("QuestionID").HasMaxLength(36).IsUnicode(false);
        builder.Property(item => item.QuestionContent).HasColumnName("QuestionContent");
        builder.Property(item => item.QuestionAnswer).HasColumnName("QuestionAnswer");
        builder.Property(item => item.AnswersSnapshot).HasColumnName("AnswersSnapshot");
        builder.Property(item => item.PictureUrl).HasColumnName("PictureUrl");
        builder.Property(item => item.SnapshotSchemaVersion).HasColumnName("SnapshotSchemaVersion");
    }

    private static void ConfigureSession(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<TestSession>();
        builder.ToTable("TestSession", table => table.ExcludeFromMigrations());
        builder.HasKey(item => item.SessionId);
        builder.Property(item => item.SessionId).HasColumnName("SessionID").HasMaxLength(36).IsUnicode(false);
        builder.Property(item => item.TestId).HasColumnName("TestID").HasMaxLength(36).IsUnicode(false);
        builder.Property(item => item.StudentId).HasColumnName("StudentID").HasMaxLength(36).IsUnicode(false);
        builder.Property(item => item.TestFormat).HasColumnName("TestFormat").HasMaxLength(20).IsUnicode(false);
        builder.Property(item => item.Status).HasColumnName("Status").HasMaxLength(20).IsUnicode(false);
        builder.Property(item => item.SubmissionType).HasColumnName("SubmissionType").HasMaxLength(30).IsUnicode(false);
        builder.Property(item => item.Duration).HasColumnName("Duration");
        builder.Property(item => item.StartTime).HasColumnName("StartTime");
        builder.Property(item => item.EndTime).HasColumnName("EndTime");
        builder.Property(item => item.TotalQuestion).HasColumnName("TotalQuestion");
        builder.Property(item => item.NumCorrect).HasColumnName("NumCorrect");
        builder.Property(item => item.NumIncorrect).HasColumnName("NumIncorrect");
        builder.Property(item => item.NumAbandoned).HasColumnName("NumAbandoned");
        builder.Property(item => item.Score).HasColumnName("Score").HasPrecision(5, 2);
        builder.Property(item => item.GradeRevision).HasColumnName("GradeRevision");
    }

    private static void ConfigureAnswers(ModelBuilder modelBuilder)
    {
        var answer = modelBuilder.Entity<TestAnswer>();
        answer.ToTable("TestAnswer", table => table.ExcludeFromMigrations());
        answer.HasKey(item => item.TestAnswerId);
        answer.Property(item => item.TestAnswerId).HasColumnName("TestAnswerID").HasMaxLength(36).IsUnicode(false);
        answer.Property(item => item.SessionId).HasColumnName("SessionID").HasMaxLength(36).IsUnicode(false);
        answer.Property(item => item.QuestionId).HasColumnName("QuestionID").HasMaxLength(36).IsUnicode(false);
        answer.Property(item => item.AnswerId).HasColumnName("AnswerID").HasMaxLength(36).IsUnicode(false);
        answer.Property(item => item.QuestionNo).HasColumnName("QuestionNo");
        answer.Property(item => item.TimeSpent).HasColumnName("TimeSpent");
        answer.Property(item => item.FirstChoiceTime).HasColumnName("FirstChoiceTime");
        answer.Property(item => item.UpdateChoiceTime).HasColumnName("UpdateChoiceTime");
        answer.Property(item => item.ShortAnswerText).HasColumnName("ShortAnswerText");
        answer.Property(item => item.IsCorrect).HasColumnName("IsCorrect");
        answer.Property(item => item.PointsEarned).HasColumnName("PointsEarned").HasPrecision(5, 2);

        var option = modelBuilder.Entity<TestAnswerOption>();
        option.ToTable("TestAnswerOption", table => table.ExcludeFromMigrations());
        option.HasKey(item => new { item.TestAnswerId, item.AnswerId });
        option.Property(item => item.TestAnswerId).HasColumnName("TestAnswerID").HasMaxLength(36).IsUnicode(false);
        option.Property(item => item.AnswerId).HasColumnName("AnswerID").HasMaxLength(36).IsUnicode(false);

        var part = modelBuilder.Entity<TestAnswerPart>();
        part.ToTable("TestAnswerPart", table => table.ExcludeFromMigrations());
        part.HasKey(item => new { item.TestAnswerId, item.PartId });
        part.Property(item => item.TestAnswerId).HasColumnName("TestAnswerID").HasMaxLength(36).IsUnicode(false);
        part.Property(item => item.PartId).HasColumnName("PartID").HasMaxLength(36).IsUnicode(false);
        part.Property(item => item.BooleanAnswer).HasColumnName("BooleanAnswer");
        part.Property(item => item.TextAnswer).HasColumnName("TextAnswer");
        part.Property(item => item.NumericAnswer).HasColumnName("NumericAnswer").HasPrecision(18, 6);
        part.Property(item => item.IsCorrect).HasColumnName("IsCorrect");
        part.Property(item => item.PointsEarned).HasColumnName("PointsEarned").HasPrecision(5, 2);
    }
}
