using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;

namespace MathInsight.Modules.Grading_Analytics.Persistence.Configurations;

public class TagDifficultyReadOnlyConfiguration : IEntityTypeConfiguration<TagDifficultyReadOnly>
{
    public void Configure(EntityTypeBuilder<TagDifficultyReadOnly> builder)
    {
        builder.ToTable("TagDifficulty", table => table.ExcludeFromMigrations());
        builder.HasKey(x => x.DifficultyId);

        builder.Property(x => x.DifficultyId)
            .HasColumnName("DifficultyID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(x => x.LevelValue)
            .HasColumnName("LevelValue")
            .IsRequired();
    }
}
