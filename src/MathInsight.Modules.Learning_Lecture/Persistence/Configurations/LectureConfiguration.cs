using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Learning_Lecture.Entities;

namespace MathInsight.Modules.Learning_Lecture.Persistence.Configurations;

public class LectureConfiguration : IEntityTypeConfiguration<Lecture>
{
    public void Configure(EntityTypeBuilder<Lecture> builder)
    {
        builder.ToTable("lectures");

        builder.HasKey(x => x.LectureId);
        
        builder.Property(x => x.LectureId).HasColumnName("lecture_id").HasMaxLength(36);
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Content).HasColumnName("content");
        builder.Property(x => x.VideoUrl).HasColumnName("video_url").HasMaxLength(255);
        builder.Property(x => x.ThumbnailUrl).HasColumnName("thumbnail_url").HasMaxLength(255);
        builder.Property(x => x.Likes).HasColumnName("Likes");
        builder.Property(x => x.TeacherId).HasColumnName("teacher_id").HasMaxLength(36).IsRequired();
        builder.Property(x => x.TagId).HasColumnName("tag_id").HasMaxLength(36).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired().HasDefaultValue("Draft");
        builder.Property(x => x.CreatedTime).HasColumnName("created_time");
        builder.Property(x => x.UpdatedTime).HasColumnName("updated_time");

        builder.HasIndex(x => new { x.Status, x.TagId });

        builder.HasMany(x => x.LectureMaterials)
            .WithOne(x => x.Lecture)
            .HasForeignKey(x => x.LectureId);

        builder.HasMany(x => x.LectureLikes)
            .WithOne(x => x.Lecture)
            .HasForeignKey(x => x.LectureId);

        builder.HasMany(x => x.DiscussionQuestions)
            .WithOne(x => x.Lecture)
            .HasForeignKey(x => x.LectureId);
    }
}
