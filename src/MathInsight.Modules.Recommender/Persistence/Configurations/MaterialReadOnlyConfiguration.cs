using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Recommender.Persistence.Entities;

namespace MathInsight.Modules.Recommender.Persistence.Configurations;

/// <summary>
/// Read-only EF configuration for cross-module Material table.
/// Used for recommendation queries (UC-54) — no writes allowed.
/// </summary>
public class MaterialReadOnlyConfiguration : IEntityTypeConfiguration<MaterialReadOnly>
{
    public void Configure(EntityTypeBuilder<MaterialReadOnly> builder)
    {
        builder.ToTable("Material");
        builder.HasKey(x => x.MaterialId);

        builder.Property(x => x.MaterialId).HasColumnName("material_id");
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200);
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(2000);
        builder.Property(x => x.FileUrl).HasColumnName("file_url").HasMaxLength(500);
        builder.Property(x => x.MaterialType).HasColumnName("material_type").HasMaxLength(50);
        builder.Property(x => x.IsActive).HasColumnName("is_active");
    }
}
