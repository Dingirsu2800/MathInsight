using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Learning_Lecture.Entities;

namespace MathInsight.Modules.Learning_Lecture.Persistence.Configurations;

public class TagTopicReadOnlyConfiguration : IEntityTypeConfiguration<TagTopicReadOnly>
{
    public void Configure(EntityTypeBuilder<TagTopicReadOnly> builder)
    {
        builder.ToTable("TagTopic", table => table.ExcludeFromMigrations());
        builder.HasKey(x => x.TagId);
        
        builder.Property(x => x.TagId).HasColumnName("TagID").IsUnicode(false).HasMaxLength(36);
        builder.Property(x => x.TagName).HasColumnName("TagName").HasMaxLength(50);
        builder.Property(x => x.ParentTagId).HasColumnName("ParentTagID").IsUnicode(false).HasMaxLength(36);
        builder.Property(x => x.Grade).HasColumnName("Grade");
        builder.Property(x => x.IsActive).HasColumnName("IsActive");
        builder.Property(x => x.DisplayOrder).HasColumnName("DisplayOrder");
    }
}
