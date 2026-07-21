using MathInsight.Modules.Gamification.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Gamification.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Badge"/> to DB table [Badge]. ConditionType maps to the VARCHAR(50) column
/// by enum name. BadgeName is UNIQUE (spec.md key entities). BR-43.
/// </summary>
public class BadgeConfiguration : IEntityTypeConfiguration<Badge>
{
    public void Configure(EntityTypeBuilder<Badge> builder)
    {
        builder.ToTable(nameof(Badge));

        builder.HasKey(badge => badge.BadgeId);

        builder.Property(badge => badge.BadgeId)
            .HasColumnName("BadgeID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();

        // NVARCHAR(100), UNIQUE.
        builder.Property(badge => badge.BadgeName)
            .HasColumnName("BadgeName")
            .HasMaxLength(100)
            .IsRequired();

        // NVARCHAR(255) NOT NULL.
        builder.Property(badge => badge.Description)
            .HasColumnName("Description")
            .HasMaxLength(255)
            .IsRequired();

        // VARCHAR(255) NULL.
        builder.Property(badge => badge.IconUrl)
            .HasColumnName("IconUrl")
            .HasMaxLength(255)
            .IsUnicode(false);

        // VARCHAR(50), persisted by enum name.
        builder.Property(badge => badge.ConditionType)
            .HasColumnName("ConditionType")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(badge => badge.ConditionValue)
            .HasColumnName("ConditionValue");

        builder.Property(badge => badge.CreatedTime)
            .HasColumnName("CreatedTime");

        builder.HasIndex(badge => badge.BadgeName)
            .IsUnique()
            .HasDatabaseName("UQ_Badge_BadgeName");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_Badge_ConditionValue",
                "[ConditionValue] >= 0");
        });
    }
}
