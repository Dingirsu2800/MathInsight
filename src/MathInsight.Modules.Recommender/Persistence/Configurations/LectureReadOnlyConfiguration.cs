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
        builder.ToTable("Lecture");
        builder.HasKey(x => x.LectureId);

        builder.Property(x => x.LectureId).HasColumnName("lecture_id");
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200);
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(2000);
        builder.Property(x => x.TagId).HasColumnName("tag_id");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
    }
}
