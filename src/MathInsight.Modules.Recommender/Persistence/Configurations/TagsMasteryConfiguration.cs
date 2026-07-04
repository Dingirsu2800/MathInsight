using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Recommender.Persistence.Entities;

namespace MathInsight.Modules.Recommender.Persistence.Configurations;

/// <summary>
/// EF configuration for TagsMastery.
/// Maps to DB script table: TagsMastery.
/// Unique constraint: (student_id, tag_id). No difficulty_id column (RCM-01, plan.md).
/// OfficialPoint, PracticePoint, ExamAnchor range: 0.00..10.00 (RCM-02).
/// </summary>
public class TagsMasteryConfiguration : IEntityTypeConfiguration<TagsMastery>
{
    public void Configure(EntityTypeBuilder<TagsMastery> builder)
    {
        builder.ToTable("TagsMastery");
        builder.HasKey(x => x.TagsMasteryId);

        builder.Property(x => x.TagsMasteryId)
            .HasColumnName("tags_mastery_id");

        builder.Property(x => x.StudentId)
            .HasColumnName("student_id")
            .IsRequired();

        builder.Property(x => x.TagId)
            .HasColumnName("tag_id")
            .IsRequired();

        builder.Property(x => x.MasteryStatus)
            .HasColumnName("mastery_status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.NumberDone)
            .HasColumnName("number_done")
            .IsRequired();

        builder.Property(x => x.NumCorrect)
            .HasColumnName("num_correct")
            .IsRequired();

        builder.Property(x => x.AccuracyRate)
            .HasColumnName("accuracy_rate")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.OfficialPoint)
            .HasColumnName("official_point")
            .HasPrecision(4, 2)
            .IsRequired();

        builder.Property(x => x.PracticePoint)
            .HasColumnName("practice_point")
            .HasPrecision(4, 2)
            .IsRequired();

        builder.Property(x => x.ExamAnchor)
            .HasColumnName("exam_anchor")
            .HasPrecision(4, 2)
            .IsRequired();

        builder.Property(x => x.ExamHistory)
            .HasColumnName("exam_history")
            .HasMaxLength(2000);

        builder.Property(x => x.SeriesAnswerCount)
            .HasColumnName("series_answer_count")
            .IsRequired();

        builder.Property(x => x.RecommendedDifficultyLevel)
            .HasColumnName("recommended_difficulty_level")
            .IsRequired();

        builder.Property(x => x.LastCalculatedAt)
            .HasColumnName("last_calculated_at");

        // Unique (student_id, tag_id) — RCM-01
        builder.HasIndex(x => new { x.StudentId, x.TagId })
            .IsUnique()
            .HasDatabaseName("UQ_TagsMastery_Student_Tag");

        // Check constraints for point range — RCM-02
        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_TagsMastery_OfficialPoint_Range",
                "[official_point] >= 0.00 AND [official_point] <= 10.00");
            t.HasCheckConstraint(
                "CK_TagsMastery_PracticePoint_Range",
                "[practice_point] >= 0.00 AND [practice_point] <= 10.00");
            t.HasCheckConstraint(
                "CK_TagsMastery_ExamAnchor_Range",
                "[exam_anchor] >= 0.00 AND [exam_anchor] <= 10.00");
            t.HasCheckConstraint(
                "CK_TagsMastery_DifficultyLevel_Range",
                "[recommended_difficulty_level] >= 1 AND [recommended_difficulty_level] <= 4");
        });
    }
}
