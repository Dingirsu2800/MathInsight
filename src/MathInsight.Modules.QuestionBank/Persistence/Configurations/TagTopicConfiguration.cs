using MathInsight.Modules.QuestionBank.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.QuestionBank.Persistence.Configurations;

public class TagTopicConfiguration : IEntityTypeConfiguration<TagTopic>
{
    public void Configure(EntityTypeBuilder<TagTopic> builder)
    {
        builder.ToTable(nameof(TagTopic));

        builder.HasKey(topic => topic.TagId)
            .HasName("PK_TagTopic");

        builder.Property(topic => topic.TagId)
            .HasColumnName("TagID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();

        builder.Property(topic => topic.ParentTagId)
            .HasColumnName("ParentTagID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(topic => topic.TagName)
            .HasColumnName("TagName")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(topic => topic.Description)
            .HasColumnName("Description")
            .HasMaxLength(255);

        builder.Property(topic => topic.Grade)
            .HasColumnName("Grade")
            .HasDefaultValue(10)
            .IsRequired();

        builder.Property(topic => topic.IsActive)
            .HasColumnName("IsActive")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(topic => topic.DisplayOrder)
            .HasColumnName("DisplayOrder")
            .HasDefaultValue(1)
            .IsRequired();

        builder.HasIndex(topic => topic.TagName)
            .IsUnique();

        builder.HasOne(topic => topic.ParentTag)
            .WithMany(topic => topic.ChildTags)
            .HasForeignKey(topic => topic.ParentTagId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TagTopic_TagTopic_ParentTagID");
    }
}
