using MathInsight.Modules.QuestionBank.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.QuestionBank.Persistence.Configurations;

public class TagDifficultyConfiguration : IEntityTypeConfiguration<TagDifficulty>
{

    public void Configure(EntityTypeBuilder<TagDifficulty> builder)
    {
        builder.ToTable(nameof(TagDifficulty));

        builder.HasKey(difficulty => difficulty.DifficultyId);

        builder.Property(difficulty => difficulty.DifficultyId)
            .HasColumnName("DifficultyID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();

        builder.Property(difficulty => difficulty.DifficultyName)
            .HasColumnName("DifficultyName")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(difficulty => difficulty.Description)
            .HasColumnName("Description")
            .HasMaxLength(255);

        builder.Property(difficulty => difficulty.LevelValue)
            .HasColumnName("LevelValue")
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(difficulty => difficulty.DisplayOrder)
            .HasColumnName("DisplayOrder")
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(difficulty => difficulty.IsActive)
            .HasColumnName("IsActive")
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(difficulty => difficulty.DifficultyName)
            .IsUnique();

        builder.HasIndex(difficulty => difficulty.LevelValue)
            .IsUnique();
    }
}
