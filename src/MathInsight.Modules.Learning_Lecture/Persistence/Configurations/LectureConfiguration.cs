using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Learning_Lecture.Entities;

namespace MathInsight.Modules.Learning_Lecture.Persistence.Configurations;

public class LectureConfiguration : IEntityTypeConfiguration<Lecture>
{
    public void Configure(EntityTypeBuilder<Lecture> builder)
    {
        builder.ToTable(nameof(Lecture));

        builder.HasKey(x => x.LectureId);
        
        builder.Property(x => x.LectureId).HasColumnName("LectureID").IsUnicode(false).HasMaxLength(36);
        builder.Property(x => x.Title).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Content);
        builder.Property(x => x.VideoUrl).IsUnicode(false).HasMaxLength(255);
        builder.Property(x => x.ThumbnailUrl).IsUnicode(false).HasMaxLength(255);
        builder.Property(x => x.Likes);
        builder.Property(x => x.TeacherId).HasColumnName("TeacherID").IsUnicode(false).HasMaxLength(36).IsRequired();
        builder.Property(x => x.TagId).HasColumnName("TagID").IsUnicode(false).HasMaxLength(36).IsRequired();
        builder.Property(x => x.DifficultyId).HasColumnName("DifficultyID").IsUnicode(false).HasMaxLength(36);
        builder.Property(x => x.Status).IsUnicode(false).HasMaxLength(20).IsRequired().HasDefaultValue("Draft");
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);

        builder.HasIndex(x => new { x.Status, x.TagId, x.DifficultyId })
            .HasDatabaseName("IX_Lecture_Status_TagID_DifficultyID");

        builder.HasMany(x => x.LectureMaterials)
            .WithOne(x => x.Lecture)
            .HasForeignKey(x => x.LectureId);

        builder.HasMany(x => x.LectureLikes)
            .WithOne(x => x.Lecture)
            .HasForeignKey(x => x.LectureId);

        builder.HasMany(x => x.DiscussionQuestions)
            .WithOne(x => x.Lecture)
            .HasForeignKey(x => x.LectureId);

        builder.Property(x => x.NextLectureId).HasColumnName("NextLectureID").IsUnicode(false).HasMaxLength(36);

        builder.HasOne(x => x.NextLecture)
            .WithMany()
            .HasForeignKey(x => x.NextLectureId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
