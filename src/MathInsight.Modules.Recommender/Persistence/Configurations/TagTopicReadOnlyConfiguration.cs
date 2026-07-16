using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Recommender.Persistence.Entities;

namespace MathInsight.Modules.Recommender.Persistence.Configurations;

/// <summary>
/// Read-only EF configuration for cross-module TagTopic table owned by QuestionBank.
/// Used by Recommender to resolve tag names for DTOs without a project reference to QuestionBank.
/// TagTopic.TagId and all Recommender identifiers use the canonical VARCHAR(36)/string contract.
/// </summary>
public class TagTopicReadOnlyConfiguration : IEntityTypeConfiguration<TagTopicReadOnly>
{
    public void Configure(EntityTypeBuilder<TagTopicReadOnly> builder)
    {
        builder.ToTable("TagTopic", table => table.ExcludeFromMigrations());
        builder.HasKey(x => x.TagId).HasName("PK_TagTopic");

        builder.Property(x => x.TagId)
            .HasColumnName("TagID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();

        builder.Property(x => x.TagName)
            .HasColumnName("TagName")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Grade)
            .HasColumnName("Grade");

        builder.Property(x => x.IsActive)
            .HasColumnName("IsActive");
    }
}
