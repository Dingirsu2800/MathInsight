using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Learning_Lecture.Entities;

namespace MathInsight.Modules.Learning_Lecture.Persistence.Configurations;

public class TagTopicReadOnlyConfiguration : IEntityTypeConfiguration<TagTopicReadOnly>
{
    public void Configure(EntityTypeBuilder<TagTopicReadOnly> builder)
    {
        builder.ToTable("TagTopic");
        builder.HasKey(x => x.TagId);
        
        builder.Property(x => x.TagId).HasColumnName("TagID").HasMaxLength(36);
        builder.Property(x => x.TagName).HasColumnName("TagName").HasMaxLength(50);
        builder.Property(x => x.ParentTagId).HasColumnName("ParentTagID").HasMaxLength(36);
        builder.Property(x => x.Grade).HasColumnName("Grade");
    }
}
