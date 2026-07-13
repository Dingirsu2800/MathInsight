using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Learning_Lecture.Entities;

namespace MathInsight.Modules.Learning_Lecture.Persistence.Configurations;

public class TagTopicReadOnlyConfiguration : IEntityTypeConfiguration<TagTopicReadOnly>
{
    public void Configure(EntityTypeBuilder<TagTopicReadOnly> builder)
    {
        builder.ToTable("tag_topics");
        builder.HasKey(x => x.TagId);
        
        builder.Property(x => x.TagId).HasColumnName("tag_id").HasMaxLength(36);
        builder.Property(x => x.TagName).HasColumnName("tag_name").HasMaxLength(100);
        builder.Property(x => x.ParentTagId).HasColumnName("parent_tag_id").HasMaxLength(36);
        builder.Property(x => x.Grade).HasColumnName("grade");
    }
}
