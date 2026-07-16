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
        builder.HasKey(x => x.LectureId).HasName("PK_Lecture");

        builder.Property(x => x.LectureId)
            .HasColumnName("LectureID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();
        builder.Property(x => x.Title)
            .HasColumnName("Title")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(x => x.Content)
            .HasColumnName("Content");
        builder.Property(x => x.TagId)
            .HasColumnName("TagID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(x => x.Status)
            .HasColumnName("Status")
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();
    }
}
