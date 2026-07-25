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
            .HasColumnName("TagsMasteryID")
            .HasMaxLength(36);

        builder.Property(x => x.StudentId)
            .HasColumnName("StudentID")
            .HasMaxLength(36)
            .IsRequired();

        builder.Property(x => x.TagId)
            .HasColumnName("TagID")
            .HasMaxLength(36)
            .IsRequired();

        builder.Property(x => x.MasteryStatus)
            .HasColumnName("MasteryStatus")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.NumberDone)
            .HasColumnName("NumberDone")
            .IsRequired();

        builder.Property(x => x.NumCorrect)
            .HasColumnName("NumCorrect")
            .IsRequired();

        builder.Property(x => x.AccuracyRate)
            .HasColumnName("AccuracyRate")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.OfficialPoint)
            .HasColumnName("OfficialPoint")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.PracticePoint)
            .HasColumnName("PracticePoint")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.ExamAnchor)
            .HasColumnName("ExamAnchor")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.ExamHistory)
            .HasColumnName("ExamHistory")
            .HasMaxLength(2000);

        builder.Property(x => x.SeriesAnswerCount)
            .HasColumnName("SeriesAnswerCount")
            .IsRequired();

        builder.Property(x => x.RecommendedDifficultyLevel)
            .HasColumnName("RecommendedDifficultyLevel")
            .IsRequired();

        builder.Property(x => x.LastPracticedTime)
            .HasColumnName("LastPracticedTime");

        builder.Property(x => x.LastCalculatedAt)
            .HasColumnName("LastCalculatedAt");

        // Unique (StudentID, TagID) — RCM-01
        builder.HasIndex(x => new { x.StudentId, x.TagId })
            .IsUnique()
            .HasDatabaseName("UQ_TagsMastery_Student_Tag");

        // Check constraints for point range — RCM-02
        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_TagsMastery_OfficialPoint_Range",
                "[OfficialPoint] >= 0.00 AND [OfficialPoint] <= 10.00");
            t.HasCheckConstraint(
                "CK_TagsMastery_PracticePoint_Range",
                "[PracticePoint] >= 0.00 AND [PracticePoint] <= 10.00");
            t.HasCheckConstraint(
                "CK_TagsMastery_ExamAnchor_Range",
                "[ExamAnchor] >= 0.00 AND [ExamAnchor] <= 10.00");
            t.HasCheckConstraint(
                "CK_TagsMastery_DifficultyLevel_Range",
                "[RecommendedDifficultyLevel] >= 1 AND [RecommendedDifficultyLevel] <= 4");
        });
    }
}
