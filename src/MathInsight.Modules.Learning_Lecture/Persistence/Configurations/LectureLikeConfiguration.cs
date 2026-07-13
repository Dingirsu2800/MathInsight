using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Learning_Lecture.Entities;

namespace MathInsight.Modules.Learning_Lecture.Persistence.Configurations;

public class LectureLikeConfiguration : IEntityTypeConfiguration<LectureLike>
{
    public void Configure(EntityTypeBuilder<LectureLike> builder)
    {
        builder.ToTable("lecture_likes");

        builder.HasKey(x => new { x.LectureId, x.StudentId });

        builder.Property(x => x.LectureId).HasColumnName("lecture_id").HasMaxLength(36);
        builder.Property(x => x.StudentId).HasColumnName("student_id").HasMaxLength(36);
        builder.Property(x => x.CreatedTime).HasColumnName("created_time");
    }
}
