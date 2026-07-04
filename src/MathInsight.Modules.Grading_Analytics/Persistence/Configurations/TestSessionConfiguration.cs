using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;

namespace MathInsight.Modules.Grading_Analytics.Persistence.Configurations;

public class TestSessionConfiguration : IEntityTypeConfiguration<TestSession>
{
    public void Configure(EntityTypeBuilder<TestSession> builder)
    {
        builder.ToTable("TestSessions");
        builder.HasKey(x => x.SessionId);

        builder.Property(x => x.SessionId).HasColumnName("session_id");
        builder.Property(x => x.TestId).HasColumnName("test_id");
        builder.Property(x => x.StudentId).HasColumnName("student_id");
        builder.Property(x => x.TestFormat).HasColumnName("test_format").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(x => x.SubmissionType).HasColumnName("submission_type").HasMaxLength(20);
        builder.Property(x => x.Duration).HasColumnName("duration");
        builder.Property(x => x.StartTime).HasColumnName("start_time");
        builder.Property(x => x.EndTime).HasColumnName("end_time");
        builder.Property(x => x.TotalQuestion).HasColumnName("total_question");
        builder.Property(x => x.NumCorrect).HasColumnName("num_correct");
        builder.Property(x => x.NumIncorrect).HasColumnName("num_incorrect");
        builder.Property(x => x.NumAbandoned).HasColumnName("num_abandoned");
        builder.Property(x => x.Score).HasColumnName("score").HasPrecision(5, 2);

        builder.HasMany(x => x.TestAnswers)
               .WithOne(a => a.Session)
               .HasForeignKey(a => a.SessionId);
    }
}
