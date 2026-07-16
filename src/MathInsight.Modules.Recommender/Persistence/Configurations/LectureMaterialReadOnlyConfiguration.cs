using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Recommender.Persistence.Entities;

namespace MathInsight.Modules.Recommender.Persistence.Configurations;

/// <summary>
/// Read-only EF configuration for cross-module LectureMaterial join table.
/// Many-to-many relationship between Lecture and Material per current ERD.
/// Used for recommendation queries (UC-54) — no writes allowed.
/// </summary>
public class LectureMaterialReadOnlyConfiguration : IEntityTypeConfiguration<LectureMaterialReadOnly>
{
    public void Configure(EntityTypeBuilder<LectureMaterialReadOnly> builder)
    {
        builder.ToTable("LectureMaterial");
        builder.HasKey(x => new { x.LectureId, x.MaterialId });

        builder.Property(x => x.LectureId).HasColumnName("LectureID").HasMaxLength(36);
        builder.Property(x => x.MaterialId).HasColumnName("MaterialID").HasMaxLength(36);
    }
}
