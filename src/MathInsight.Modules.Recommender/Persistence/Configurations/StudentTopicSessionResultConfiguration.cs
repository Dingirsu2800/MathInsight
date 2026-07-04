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
            .HasColumnName("student_topic_session_result_id");

        builder.Property(x => x.StudentId)
            .HasColumnName("student_id")
            .IsRequired();

        builder.Property(x => x.SessionId)
            .HasColumnName("session_id")
            .IsRequired();

        builder.Property(x => x.TagId)
            .HasColumnName("tag_id")
            .IsRequired();

        builder.Property(x => x.TotalQuestions)
            .HasColumnName("total_questions")
            .IsRequired();

        builder.Property(x => x.CorrectCount)
            .HasColumnName("correct_count")
            .IsRequired();

        builder.Property(x => x.WrongCount)
            .HasColumnName("wrong_count")
            .IsRequired();

        builder.Property(x => x.TopicScore)
            .HasColumnName("topic_score")
            .HasPrecision(4, 2)
            .IsRequired();

        builder.Property(x => x.PointBefore)
            .HasColumnName("point_before")
            .HasPrecision(4, 2)
            .IsRequired();

        builder.Property(x => x.PointAfter)
            .HasColumnName("point_after")
            .HasPrecision(4, 2)
            .IsRequired();

        builder.Property(x => x.CreatedTime)
            .HasColumnName("created_time")
            .IsRequired();

        // Unique (session_id, tag_id) — idempotency key (RCM-08)
        builder.HasIndex(x => new { x.SessionId, x.TagId })
            .IsUnique()
            .HasDatabaseName("UQ_StudentTopicSessionResult_Session_Tag");

        // Non-negative counts and score range
        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_STSR_TotalQuestions_NonNeg",
                "[total_questions] >= 0");
            t.HasCheckConstraint(
                "CK_STSR_CorrectCount_NonNeg",
                "[correct_count] >= 0");
            t.HasCheckConstraint(
                "CK_STSR_WrongCount_NonNeg",
                "[wrong_count] >= 0");
            t.HasCheckConstraint(
                "CK_STSR_TopicScore_Range",
                "[topic_score] >= 0.00 AND [topic_score] <= 10.00");
        });
    }
}
