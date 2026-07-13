using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Learning_Lecture.Entities;

namespace MathInsight.Modules.Learning_Lecture.Persistence.Configurations;

public class LectureMaterialConfiguration : IEntityTypeConfiguration<LectureMaterial>
{
    public void Configure(EntityTypeBuilder<LectureMaterial> builder)
    {
        builder.ToTable("lecture_materials");

        builder.HasKey(x => new { x.LectureId, x.MaterialId });

        builder.Property(x => x.LectureId).HasColumnName("lecture_id").HasMaxLength(36);
        builder.Property(x => x.MaterialId).HasColumnName("material_id").HasMaxLength(36);
    }
}
