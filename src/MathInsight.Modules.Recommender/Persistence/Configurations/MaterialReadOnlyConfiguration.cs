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
        builder.ToTable("Material", table => table.ExcludeFromMigrations());
        builder.HasKey(x => x.MaterialId).HasName("PK_Material");

        builder.Property(x => x.MaterialId)
            .HasColumnName("MaterialID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();
        builder.Property(x => x.MaterialName)
            .HasColumnName("MaterialName")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(x => x.FileUrl)
            .HasColumnName("FileUrl")
            .HasMaxLength(255)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(x => x.FileType)
            .HasColumnName("FileType")
            .HasMaxLength(10)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(x => x.Status)
            .HasColumnName("Status")
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();
    }
}
