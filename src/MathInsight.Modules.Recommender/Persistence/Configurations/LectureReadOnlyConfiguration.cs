using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Recommender.Persistence.Entities;

namespace MathInsight.Modules.Recommender.Persistence.Configurations;

/// <summary>
/// Read-only EF configuration for cross-module Lecture table.
/// Maps to the existing Lecture table owned by Learning_Lecture module.
/// Used for recommendation queries (UC-53) — no writes allowed.
/// </summary>
public class LectureReadOnlyConfiguration : IEntityTypeConfiguration<LectureReadOnly>
{
    public void Configure(EntityTypeBuilder<LectureReadOnly> builder)
    {
        builder.ToTable("Lecture", table => table.ExcludeFromMigrations());
        builder.HasKey(x => x.LectureId);

        builder.Property(x => x.LectureId).HasColumnName("LectureID").IsUnicode(false).HasMaxLength(36);
        builder.Property(x => x.Title).HasColumnName("Title").HasMaxLength(100);
        builder.Property(x => x.ThumbnailUrl).HasColumnName("ThumbnailUrl").IsUnicode(false).HasMaxLength(255);
        builder.Property(x => x.Likes).HasColumnName("Likes");
        builder.Property(x => x.TagId).HasColumnName("TagID").IsUnicode(false).HasMaxLength(36);
        builder.Property(x => x.DifficultyId).HasColumnName("DifficultyID").IsUnicode(false).HasMaxLength(36);
        builder.Property(x => x.Status).HasColumnName("Status").IsUnicode(false).HasMaxLength(20);
        builder.Property(x => x.UpdatedTime).HasColumnName("UpdatedTime");
    }
}
