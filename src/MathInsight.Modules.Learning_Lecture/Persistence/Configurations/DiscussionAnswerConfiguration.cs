using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Learning_Lecture.Entities;

namespace MathInsight.Modules.Learning_Lecture.Persistence.Configurations;

public class DiscussionAnswerConfiguration : IEntityTypeConfiguration<DiscussionAnswer>
{
    public void Configure(EntityTypeBuilder<DiscussionAnswer> builder)
    {
        builder.ToTable(nameof(DiscussionAnswer));

        builder.HasKey(x => x.DiscussionAnswerId);

        builder.Property(x => x.DiscussionAnswerId).HasMaxLength(36);
        builder.Property(x => x.DiscussionQuestionId).HasMaxLength(36).IsRequired();
        builder.Property(x => x.AccountId).HasMaxLength(36).IsRequired();
        builder.Property(x => x.Content).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Active");
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.UpdatedTime);

        builder.HasMany(x => x.Reports)
            .WithOne(x => x.Answer)
            .HasForeignKey(x => x.DiscussionAnswerId);
    }
}
