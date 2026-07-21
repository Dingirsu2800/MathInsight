using MathInsight.Modules.Gamification.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Gamification.Persistence.Configurations;

/// <summary>
/// Maps <see cref="TargetScore"/> to DB table [TargetScore]. UNIQUE (StudentID, TagID) — one
/// target per topic tag (BR-44). TargetPoint is DECIMAL(4,2), range [0, 10] (DC-04).
/// </summary>
public class TargetScoreConfiguration : IEntityTypeConfiguration<TargetScore>
{
    public void Configure(EntityTypeBuilder<TargetScore> builder)
    {
        builder.ToTable(nameof(TargetScore));

        builder.HasKey(targetScore => targetScore.TargetId);

        builder.Property(targetScore => targetScore.TargetId)
            .HasColumnName("TargetID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();

        builder.Property(targetScore => targetScore.StudentId)
            .HasColumnName("StudentID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(targetScore => targetScore.TagId)
            .HasColumnName("TagID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(targetScore => targetScore.TargetPoint)
            .HasColumnName("TargetPoint")
            .HasColumnType("decimal(4,2)")
            .IsRequired();

        builder.Property(targetScore => targetScore.CreatedTime)
            .HasColumnName("CreatedTime");

        builder.Property(targetScore => targetScore.UpdatedTime)
            .HasColumnName("UpdatedTime");

        // BR-44: one target per (student, tag). Matches UQ_TargetScore_Student_Tag in the DDL.
        builder.HasIndex(targetScore => new { targetScore.StudentId, targetScore.TagId })
            .IsUnique()
            .HasDatabaseName("UQ_TargetScore_Student_Tag");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_TargetScore_TargetPoint",
                "[TargetPoint] >= 0 AND [TargetPoint] <= 10");
        });
    }
}
