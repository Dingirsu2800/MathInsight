using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Learning_Lecture.Entities;

namespace MathInsight.Modules.Learning_Lecture.Persistence.Configurations;

public class DiscussionQuestionConfiguration : IEntityTypeConfiguration<DiscussionQuestion>
{
    public void Configure(EntityTypeBuilder<DiscussionQuestion> builder)
    {
        builder.ToTable("discussion_questions");

        builder.HasKey(x => x.DiscussionQuestionId);

        builder.Property(x => x.DiscussionQuestionId).HasColumnName("discussion_question_id").HasMaxLength(36);
        builder.Property(x => x.LectureId).HasColumnName("lecture_id").HasMaxLength(36).IsRequired();
        builder.Property(x => x.StudentId).HasColumnName("student_id").HasMaxLength(36).IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(150).IsRequired();
        builder.Property(x => x.Content).HasColumnName("content").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired().HasDefaultValue("Active");
        builder.Property(x => x.CreatedTime).HasColumnName("created_time");
        builder.Property(x => x.UpdatedTime).HasColumnName("updated_time");

        builder.HasMany(x => x.Answers)
            .WithOne(x => x.Question)
            .HasForeignKey(x => x.DiscussionQuestionId);

        builder.HasMany(x => x.Reports)
            .WithOne(x => x.Question)
            .HasForeignKey(x => x.DiscussionQuestionId);
    }
}
