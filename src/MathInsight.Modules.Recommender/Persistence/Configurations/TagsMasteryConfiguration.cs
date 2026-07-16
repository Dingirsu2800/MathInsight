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
        builder.HasKey(x => x.TagsMasteryId).HasName("PK_TagsMastery");

        builder.Property(x => x.TagsMasteryId)
            .HasColumnName("TagsMasteryID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();

        builder.Property(x => x.StudentId)
            .HasColumnName("StudentID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.TagId)
            .HasColumnName("TagID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.MasteryStatus)
            .HasColumnName("MasteryStatus")
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("NotLearned")
            .IsRequired();

        builder.Property(x => x.NumberDone)
            .HasColumnName("NumberDone")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.NumCorrect)
            .HasColumnName("NumCorrect")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.AccuracyRate)
            .HasColumnName("AccuracyRate")
            .HasPrecision(5, 2)
            .HasDefaultValue(0.00m)
            .IsRequired();

        builder.Property(x => x.OfficialPoint)
            .HasColumnName("OfficialPoint")
            .HasPrecision(5, 2)
            .HasDefaultValue(5.00m)
            .IsRequired();

        builder.Property(x => x.PracticePoint)
            .HasColumnName("PracticePoint")
            .HasPrecision(5, 2)
            .HasDefaultValue(5.00m)
            .IsRequired();

        builder.Property(x => x.ExamAnchor)
            .HasColumnName("ExamAnchor")
            .HasPrecision(5, 2)
            .HasDefaultValue(5.00m)
            .IsRequired();

        builder.Property(x => x.ExamHistory)
            .HasColumnName("ExamHistory");

        builder.Property(x => x.SeriesAnswerCount)
            .HasColumnName("SeriesAnswerCount")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.RecommendedDifficultyLevel)
            .HasColumnName("RecommendedDifficultyLevel")
            .HasDefaultValue((byte)2)
            .IsRequired();

        builder.Property(x => x.LastCalculatedAt)
            .HasColumnName("LastCalculatedAt")
            .HasColumnType("datetime2(0)");

        builder.Property(x => x.LastPracticedTime)
            .HasColumnName("LastPracticedTime")
            .HasColumnType("datetime2(0)");

        // Unique (student_id, tag_id) — RCM-01
        builder.HasIndex(x => new { x.StudentId, x.TagId })
            .IsUnique()
            .HasDatabaseName("UQ_TagsMastery_Student_Tag");

        builder.HasIndex(x => new { x.StudentId, x.OfficialPoint, x.TagId })
            .HasDatabaseName("IX_TagsMastery_Student_OfficialPoint");

        // Check constraints for point range — RCM-02
        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_TagsMastery_Status",
                "[MasteryStatus] IN ('NotLearned', 'Learning', 'Mastered')");
            t.HasCheckConstraint(
                "CK_TagsMastery_Points",
                "[OfficialPoint] >= 0 AND [OfficialPoint] <= 10 AND [PracticePoint] >= 0 AND [PracticePoint] <= 10 AND [ExamAnchor] >= 0 AND [ExamAnchor] <= 10");
            t.HasCheckConstraint(
                "CK_TagsMastery_ExamHistoryJson",
                "[ExamHistory] IS NULL OR ISJSON([ExamHistory]) = 1");
            t.HasCheckConstraint(
                "CK_TagsMastery_SeriesAnswerCount",
                "[SeriesAnswerCount] >= 0");
            t.HasCheckConstraint(
                "CK_TagsMastery_RecommendedDifficultyLevel",
                "[RecommendedDifficultyLevel] BETWEEN 1 AND 4");
            t.HasCheckConstraint(
                "CK_TagsMastery_Progress",
                "[NumberDone] >= 0 AND [NumCorrect] >= 0 AND [NumCorrect] <= [NumberDone] AND [AccuracyRate] >= 0 AND [AccuracyRate] <= 100");
        });
    }
}
