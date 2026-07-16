using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Learning_Lecture.Entities;

namespace MathInsight.Modules.Learning_Lecture.Persistence.Configurations;

public class DiscussionQuestionConfiguration : IEntityTypeConfiguration<DiscussionQuestion>
{
    public void Configure(EntityTypeBuilder<DiscussionQuestion> builder)
    {
        builder.ToTable(nameof(DiscussionQuestion));

        builder.HasKey(x => x.DiscussionQuestionId);

        builder.Property(x => x.DiscussionQuestionId).HasMaxLength(36);
        builder.Property(x => x.LectureId).HasMaxLength(36).IsRequired();
        builder.Property(x => x.StudentId).HasMaxLength(36).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Content).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Active");
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);

        builder.HasMany(x => x.Answers)
            .WithOne(x => x.Question)
            .HasForeignKey(x => x.DiscussionQuestionId);

        builder.HasMany(x => x.Reports)
            .WithOne(x => x.Question)
            .HasForeignKey(x => x.DiscussionQuestionId);
    }
}
