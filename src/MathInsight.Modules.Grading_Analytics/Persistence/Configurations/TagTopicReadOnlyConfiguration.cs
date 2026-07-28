using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;

namespace MathInsight.Modules.Grading_Analytics.Persistence.Configurations;

public class TagTopicReadOnlyConfiguration : IEntityTypeConfiguration<TagTopicReadOnly>
{
    public void Configure(EntityTypeBuilder<TagTopicReadOnly> builder)
    {
        builder.ToTable("TagTopic", table => table.ExcludeFromMigrations());
        builder.HasKey(x => x.TagId);

        builder.Property(x => x.TagId)
            .HasColumnName("TagID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(x => x.TagName)
            .HasColumnName("TagName")
            .HasMaxLength(100);

        builder.Property(x => x.Grade)
            .HasColumnName("Grade");
    }
}
