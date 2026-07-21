using MathInsight.Modules.Testing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Testing.Persistence.Configurations;

public class TestSessionConfiguration : IEntityTypeConfiguration<TestSession>
{
    public void Configure(EntityTypeBuilder<TestSession> builder)
    {
        builder.ToTable("TestSession", table =>
        {
            table.HasCheckConstraint("CK_TestSession_Status", "[Status] IN ('InProgress', 'Graded', 'Abandoned')");
            table.HasCheckConstraint("CK_TestSession_SubmissionType", "[SubmissionType] IS NULL OR [SubmissionType] IN ('StudentSubmit', 'TimeoutSubmit', 'SystemSubmit')");
            table.HasCheckConstraint("CK_TestSession_SubmissionType_Status", "([Status] = 'Graded' AND [SubmissionType] IS NOT NULL) OR ([Status] IN ('InProgress', 'Abandoned') AND [SubmissionType] IS NULL)");
            table.HasCheckConstraint("CK_TestSession_TestFormat", "[TestFormat] IN ('Practice', 'Exam', 'PRACTICE', 'EXAM')");
            table.HasCheckConstraint("CK_TestSession_Duration", "[Duration] >= 0");
            table.HasCheckConstraint("CK_TestSession_Counts", "[TotalQuestion] >= 0 AND [NumCorrect] >= 0 AND [NumIncorrect] >= 0 AND [NumAbandoned] >= 0 AND [NumCorrect] + [NumIncorrect] + [NumAbandoned] <= [TotalQuestion]");
            table.HasCheckConstraint("CK_TestSession_Score", "[Score] >= 0 AND [Score] <= 10");
            table.HasCheckConstraint("CK_TestSession_Time", "[EndTime] IS NULL OR [EndTime] >= [StartTime]");
        });

        builder.HasKey(x => x.SessionId).HasName("PK_TestSession");

        builder.Property(x => x.SessionId)
            .HasColumnName("SessionID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(x => x.TestId)
            .HasColumnName("TestID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(x => x.StudentId)
            .HasColumnName("StudentID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(x => x.TestFormat)
            .HasColumnName("TestFormat")
            .HasMaxLength(20)
            .IsUnicode(false);

        builder.Property(x => x.Status)
            .HasColumnName("Status")
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("InProgress");

        builder.Property(x => x.SubmissionType)
            .HasColumnName("SubmissionType")
            .HasMaxLength(30)
            .IsUnicode(false);

        builder.Property(x => x.Duration)
            .HasColumnName("Duration")
            .HasDefaultValue(0);

        builder.Property(x => x.StartTime)
            .HasColumnName("StartTime")
            .HasColumnType("datetime2(0)")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(x => x.EndTime)
            .HasColumnName("EndTime")
            .HasColumnType("datetime2(0)");

        builder.Property(x => x.TotalQuestion)
            .HasColumnName("TotalQuestion")
            .HasDefaultValue(0);

        builder.Property(x => x.NumCorrect)
            .HasColumnName("NumCorrect")
            .HasDefaultValue(0);

        builder.Property(x => x.NumIncorrect)
            .HasColumnName("NumIncorrect")
            .HasDefaultValue(0);

        builder.Property(x => x.NumAbandoned)
            .HasColumnName("NumAbandoned")
            .HasDefaultValue(0);

        builder.Property(x => x.Score)
            .HasColumnName("Score")
            .HasColumnType("decimal(5,2)")
            .HasDefaultValue(0.00m);

        builder.HasOne(x => x.Test)
            .WithMany()
            .HasForeignKey(x => x.TestId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TestSession_Test_TestID");

        builder.HasIndex(x => new { x.StudentId, x.Status })
            .HasDatabaseName("IX_TestSession_Student_Status");

        builder.HasIndex(x => new { x.StudentId, x.StartTime })
            .HasDatabaseName("IX_TestSession_Student_StartTime");

        builder.HasIndex(x => x.TestId)
            .HasDatabaseName("IX_TestSession_TestID");
    }
}
