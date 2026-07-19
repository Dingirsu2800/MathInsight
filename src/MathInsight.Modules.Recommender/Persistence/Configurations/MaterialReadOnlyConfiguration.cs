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

        builder.Property(x => x.MaterialId).HasColumnName("MaterialID").HasMaxLength(36);
        builder.Property(x => x.MaterialName).HasColumnName("MaterialName").HasMaxLength(100);
        builder.Property(x => x.FileUrl).HasColumnName("FileUrl").HasMaxLength(255);
        builder.Property(x => x.FileType).HasColumnName("FileType").HasMaxLength(10);
        builder.Property(x => x.Status).HasColumnName("Status").HasMaxLength(20);
    }
}
