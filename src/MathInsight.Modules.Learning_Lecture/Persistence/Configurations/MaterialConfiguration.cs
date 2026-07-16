using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Learning_Lecture.Entities;

namespace MathInsight.Modules.Learning_Lecture.Persistence.Configurations;

public class MaterialConfiguration : IEntityTypeConfiguration<Material>
{
    public void Configure(EntityTypeBuilder<Material> builder)
    {
        builder.ToTable(nameof(Material));

        builder.HasKey(x => x.MaterialId);
        
        builder.Property(x => x.MaterialId).HasMaxLength(36);
        builder.Property(x => x.MaterialName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.FileUrl).HasMaxLength(255).IsRequired();
        builder.Property(x => x.FileType).HasMaxLength(10).IsRequired();
        builder.Property(x => x.TeacherId).HasMaxLength(36).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Active");
        builder.Property(x => x.UploadedTime);

        builder.HasMany(x => x.LectureMaterials)
            .WithOne(x => x.Material)
            .HasForeignKey(x => x.MaterialId);
    }
}
