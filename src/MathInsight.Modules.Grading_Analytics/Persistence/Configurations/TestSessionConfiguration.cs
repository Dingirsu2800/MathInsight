using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;

namespace MathInsight.Modules.Grading_Analytics.Persistence.Configurations;

public class TestSessionConfiguration : IEntityTypeConfiguration<TestSession>
{
    public void Configure(EntityTypeBuilder<TestSession> builder)
    {
        builder.ToTable("TestSession");
        builder.HasKey(x => x.SessionId);

        builder.Property(x => x.SessionId).HasColumnName("SessionID");
        builder.Property(x => x.TestId).HasColumnName("TestID");
        builder.Property(x => x.StudentId).HasColumnName("StudentID");
        builder.Property(x => x.TestFormat).HasColumnName("TestFormat").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasColumnName("Status").HasMaxLength(20).IsRequired();
        builder.Property(x => x.SubmissionType).HasColumnName("SubmissionType").HasMaxLength(30);
        builder.Property(x => x.Duration).HasColumnName("Duration");
        builder.Property(x => x.StartTime).HasColumnName("StartTime");
        builder.Property(x => x.EndTime).HasColumnName("EndTime");
        builder.Property(x => x.TotalQuestion).HasColumnName("TotalQuestion");
        builder.Property(x => x.NumCorrect).HasColumnName("NumCorrect");
        builder.Property(x => x.NumIncorrect).HasColumnName("NumIncorrect");
        builder.Property(x => x.NumAbandoned).HasColumnName("NumAbandoned");
        builder.Property(x => x.Score).HasColumnName("Score").HasPrecision(5, 2);

        builder.HasMany(x => x.TestAnswers)
               .WithOne(a => a.Session)
               .HasForeignKey(a => a.SessionId);
    }
}
