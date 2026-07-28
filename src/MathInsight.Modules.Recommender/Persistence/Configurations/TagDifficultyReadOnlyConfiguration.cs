using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Recommender.Persistence.Entities;

namespace MathInsight.Modules.Recommender.Persistence.Configurations;

public class TagDifficultyReadOnlyConfiguration : IEntityTypeConfiguration<TagDifficultyReadOnly>
{
    public void Configure(EntityTypeBuilder<TagDifficultyReadOnly> builder)
    {
        builder.ToTable("TagDifficulty");
        builder.HasKey(x => x.DifficultyId);

        builder.Property(x => x.DifficultyId)
            .HasColumnName("DifficultyID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(x => x.LevelValue)
            .HasColumnName("LevelValue");

        builder.Property(x => x.DifficultyName)
            .HasColumnName("DifficultyName")
            .HasMaxLength(50);
    }
}
