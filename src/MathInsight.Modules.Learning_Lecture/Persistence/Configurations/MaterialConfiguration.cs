using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Learning_Lecture.Entities;

namespace MathInsight.Modules.Learning_Lecture.Persistence.Configurations;

public class MaterialConfiguration : IEntityTypeConfiguration<Material>
{
    public void Configure(EntityTypeBuilder<Material> builder)
    {
        builder.ToTable("materials");

        builder.HasKey(x => x.MaterialId);
        
        builder.Property(x => x.MaterialId).HasColumnName("material_id").HasMaxLength(36);
        builder.Property(x => x.MaterialName).HasColumnName("material_name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.FileUrl).HasColumnName("file_url").HasMaxLength(255).IsRequired();
        builder.Property(x => x.FileType).HasColumnName("file_type").HasMaxLength(10).IsRequired();
        builder.Property(x => x.TeacherId).HasColumnName("teacher_id").HasMaxLength(36).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired().HasDefaultValue("Active");
        builder.Property(x => x.UploadedTime).HasColumnName("uploaded_time");

        builder.HasMany(x => x.LectureMaterials)
            .WithOne(x => x.Material)
            .HasForeignKey(x => x.MaterialId);
    }
}
