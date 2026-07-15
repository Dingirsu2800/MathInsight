using MathInsight.Modules.TestGen.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.TestGen.Persistence.Configurations;

public class TagTopicReadModelConfiguration : IEntityTypeConfiguration<TagTopicReadModel>
{
    public void Configure(EntityTypeBuilder<TagTopicReadModel> builder)
    {
        builder.ToTable("TagTopic", table => table.ExcludeFromMigrations());
        builder.HasKey(x => x.TagId).HasName("PK_TagTopic");

        builder.Property(x => x.TagId)
            .HasColumnName("TagID")
            .HasMaxLength(36)
            .IsUnicode(false);
        builder.Property(x => x.ParentTagId)
            .HasColumnName("ParentTagID")
            .HasMaxLength(36)
            .IsUnicode(false);
        builder.Property(x => x.TagName)
            .HasColumnName("TagName")
            .HasMaxLength(50)
            .IsUnicode()
            .IsRequired();
        builder.Property(x => x.Description)
            .HasColumnName("Description")
            .HasMaxLength(255)
            .IsUnicode();
        builder.Property(x => x.Grade)
            .HasColumnName("Grade")
            .HasDefaultValue(10);
        builder.Property(x => x.IsActive)
            .HasColumnName("IsActive")
            .HasDefaultValue(true);
        builder.Property(x => x.DisplayOrder)
            .HasColumnName("DisplayOrder")
            .HasDefaultValue(1);
    }
}
