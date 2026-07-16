using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Recommender.Persistence.Entities;

namespace MathInsight.Modules.Recommender.Persistence.Configurations;

/// <summary>
/// Read-only EF configuration for cross-module TagTopic table owned by QuestionBank.
/// Used by Recommender to resolve tag names for DTOs without a project reference to QuestionBank.
/// TagTopic.TagId is stored as VARCHAR(36) in the DB, but Recommender uses Guid for its own entities.
/// </summary>
public class TagTopicReadOnlyConfiguration : IEntityTypeConfiguration<TagTopicReadOnly>
{
    public void Configure(EntityTypeBuilder<TagTopicReadOnly> builder)
    {
        builder.ToTable("TagTopic");
        builder.HasKey(x => x.TagId);

        builder.Property(x => x.TagId)
            .HasColumnName("TagID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(x => x.TagName)
            .HasColumnName("TagName")
            .HasMaxLength(50);

        builder.Property(x => x.Grade)
            .HasColumnName("Grade");

        builder.Property(x => x.IsActive)
            .HasColumnName("IsActive");
    }
}
