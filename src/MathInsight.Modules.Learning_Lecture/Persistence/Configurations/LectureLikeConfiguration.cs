using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Learning_Lecture.Entities;

namespace MathInsight.Modules.Learning_Lecture.Persistence.Configurations;

public class LectureLikeConfiguration : IEntityTypeConfiguration<LectureLike>
{
    public void Configure(EntityTypeBuilder<LectureLike> builder)
    {
        builder.ToTable(nameof(LectureLike));

        builder.HasKey(x => new { x.LectureId, x.StudentId });

        builder.Property(x => x.LectureId).HasMaxLength(36);
        builder.Property(x => x.StudentId).HasMaxLength(36);
        builder.Property(x => x.CreatedTime);
    }
}
