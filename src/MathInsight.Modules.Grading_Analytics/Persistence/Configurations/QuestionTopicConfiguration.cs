using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;

namespace MathInsight.Modules.Grading_Analytics.Persistence.Configurations;

public class QuestionTopicConfiguration : IEntityTypeConfiguration<QuestionTopic>
{
    public void Configure(EntityTypeBuilder<QuestionTopic> builder)
    {
        builder.ToTable("QuestionTopic", table => table.ExcludeFromMigrations());
        builder.HasKey(x => x.QuestionTopicId);

        builder.Property(x => x.QuestionTopicId).HasColumnName("QuestionTopicID").HasMaxLength(36).IsUnicode(false);
        builder.Property(x => x.QuestionId).HasColumnName("QuestionID").HasMaxLength(36).IsUnicode(false);
        builder.Property(x => x.TagId).HasColumnName("TagID").HasMaxLength(36).IsUnicode(false);
        builder.Property(x => x.IsPrimary).HasColumnName("IsPrimary");

        builder.HasOne(x => x.Question)
               .WithMany(q => q.QuestionTopics)
               .HasForeignKey(x => x.QuestionId);
    }
}
