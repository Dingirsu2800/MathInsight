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
        builder.HasKey(x => x.StudentTopicSessionResultId);

        builder.Property(x => x.StudentTopicSessionResultId)
            .HasColumnName("StudentTopicSessionResultID")
            .HasMaxLength(36);

        builder.Property(x => x.StudentId)
            .HasColumnName("StudentID")
            .HasMaxLength(36)
            .IsRequired();

        builder.Property(x => x.SessionId)
            .HasColumnName("SessionID")
            .HasMaxLength(36)
            .IsRequired();

        builder.Property(x => x.TagId)
            .HasColumnName("TagID")
            .HasMaxLength(36)
            .IsRequired();

        builder.Property(x => x.TotalItems)
            .HasColumnName("TotalItems")
            .HasPrecision(6, 2)
            .IsRequired();

        builder.Property(x => x.CorrectItems)
            .HasColumnName("CorrectItems")
            .HasPrecision(6, 2)
            .IsRequired();

        builder.Property(x => x.EarnedPoints)
            .HasColumnName("EarnedPoints")
            .HasPrecision(6, 2)
            .IsRequired();

        builder.Property(x => x.MaxPoints)
            .HasColumnName("MaxPoints")
            .HasPrecision(6, 2)
            .IsRequired();

        builder.Property(x => x.TopicScore)
            .HasColumnName("TopicScore")
            .HasPrecision(5, 2)
            .IsRequired();
        builder.Property(x => x.GradeRevision)
            .HasColumnName("GradeRevision")
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(x => x.CreatedTime)
            .HasColumnName("CreatedTime")
            .IsRequired();

        // Unique (SessionID, TagID) — idempotency key (RCM-08)
        builder.HasIndex(x => new { x.SessionId, x.TagId })
            .IsUnique()
            .HasDatabaseName("UQ_StudentTopicSessionResult_Session_Tag");

        // Non-negative counts and score range
        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_STSR_TotalItems_NonNeg",
                "[TotalItems] >= 0");
            t.HasCheckConstraint(
                "CK_STSR_CorrectItems_NonNeg",
                "[CorrectItems] >= 0");
            t.HasCheckConstraint(
                "CK_STSR_EarnedPoints_NonNeg",
                "[EarnedPoints] >= 0");
            t.HasCheckConstraint(
                "CK_STSR_MaxPoints_NonNeg",
                "[MaxPoints] >= 0");
            t.HasCheckConstraint(
                "CK_STSR_TopicScore_Range",
                "[TopicScore] >= 0.00 AND [TopicScore] <= 10.00");
        });
    }
}
