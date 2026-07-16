using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Recommender.Persistence.Entities;

namespace MathInsight.Modules.Recommender.Persistence.Configurations;

/// <summary>
/// EF configuration for StudentTopicSessionResult.
/// Maps to DB script table: StudentTopicSessionResult.
/// Unique constraint: (session_id, tag_id) — used for idempotency guard (RCM-08).
/// All count values must be non-negative. TopicScore range: 0.00..10.00.
/// </summary>
public class StudentTopicSessionResultConfiguration : IEntityTypeConfiguration<StudentTopicSessionResult>
{
    public void Configure(EntityTypeBuilder<StudentTopicSessionResult> builder)
    {
        builder.ToTable("StudentTopicSessionResult");
        builder.HasKey(x => x.StudentTopicSessionResultId)
            .HasName("PK_StudentTopicSessionResult");

        builder.Property(x => x.StudentTopicSessionResultId)
            .HasColumnName("StudentTopicSessionResultID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();

        builder.Property(x => x.StudentId)
            .HasColumnName("StudentID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.SessionId)
            .HasColumnName("SessionID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.TagId)
            .HasColumnName("TagID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.TotalItems)
            .HasColumnName("TotalItems")
            .HasPrecision(6, 2)
            .HasDefaultValue(0.00m)
            .IsRequired();

        builder.Property(x => x.CorrectItems)
            .HasColumnName("CorrectItems")
            .HasPrecision(6, 2)
            .HasDefaultValue(0.00m)
            .IsRequired();

        builder.Property(x => x.EarnedPoints)
            .HasColumnName("EarnedPoints")
            .HasPrecision(6, 2)
            .HasDefaultValue(0.00m)
            .IsRequired();

        builder.Property(x => x.MaxPoints)
            .HasColumnName("MaxPoints")
            .HasPrecision(6, 2)
            .HasDefaultValue(0.00m)
            .IsRequired();

        builder.Property(x => x.TopicScore)
            .HasColumnName("TopicScore")
            .HasPrecision(5, 2)
            .HasDefaultValue(0.00m)
            .IsRequired();

        builder.Property(x => x.CreatedTime)
            .HasColumnName("CreatedTime")
            .HasColumnType("datetime2(0)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        // Unique (session_id, tag_id) — idempotency key (RCM-08)
        builder.HasIndex(x => new { x.SessionId, x.TagId })
            .IsUnique()
            .HasDatabaseName("UQ_StudentTopicSessionResult_Session_Tag");

        builder.HasIndex(x => new { x.StudentId, x.TagId, x.CreatedTime })
            .HasDatabaseName("IX_StudentTopicSessionResult_Student_Tag_Created");

        builder.HasIndex(x => x.SessionId)
            .HasDatabaseName("IX_StudentTopicSessionResult_SessionID");

        // Non-negative counts and score range
        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_StudentTopicSessionResult_Values",
                "[TotalItems] >= 0 AND [CorrectItems] >= 0 AND [CorrectItems] <= [TotalItems] AND [EarnedPoints] >= 0 AND [MaxPoints] >= 0 AND [EarnedPoints] <= [MaxPoints] AND [TopicScore] >= 0 AND [TopicScore] <= 10");
        });
    }
}
