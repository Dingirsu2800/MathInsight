using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Learning_Lecture.Entities;

namespace MathInsight.Modules.Learning_Lecture.Persistence.Configurations;

public class DiscussionAnswerConfiguration : IEntityTypeConfiguration<DiscussionAnswer>
{
    public void Configure(EntityTypeBuilder<DiscussionAnswer> builder)
    {
        builder.ToTable("discussion_answers");

        builder.HasKey(x => x.DiscussionAnswerId);

        builder.Property(x => x.DiscussionAnswerId).HasColumnName("discussion_answer_id").HasMaxLength(36);
        builder.Property(x => x.DiscussionQuestionId).HasColumnName("discussion_question_id").HasMaxLength(36).IsRequired();
        builder.Property(x => x.AccountId).HasColumnName("account_id").HasMaxLength(36).IsRequired();
        builder.Property(x => x.Content).HasColumnName("content").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired().HasDefaultValue("Active");
        builder.Property(x => x.CreatedTime).HasColumnName("created_time");
        builder.Property(x => x.UpdatedTime).HasColumnName("updated_time");

        builder.HasMany(x => x.Reports)
            .WithOne(x => x.Answer)
            .HasForeignKey(x => x.DiscussionAnswerId);
    }
}
