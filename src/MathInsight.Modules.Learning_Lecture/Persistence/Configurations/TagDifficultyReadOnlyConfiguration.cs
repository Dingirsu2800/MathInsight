using MathInsight.Modules.Learning_Lecture.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Learning_Lecture.Persistence.Configurations;

public sealed class TagDifficultyReadOnlyConfiguration : IEntityTypeConfiguration<TagDifficultyReadOnly>
{
    public void Configure(EntityTypeBuilder<TagDifficultyReadOnly> builder)
    {
        builder.ToTable("TagDifficulty", table => table.ExcludeFromMigrations());
        builder.HasKey(x => x.DifficultyId);

        builder.Property(x => x.DifficultyId).HasColumnName("DifficultyID").IsUnicode(false).HasMaxLength(36);
        builder.Property(x => x.DifficultyName).HasColumnName("DifficultyName").HasMaxLength(50);
        builder.Property(x => x.LevelValue).HasColumnName("LevelValue");
        builder.Property(x => x.DisplayOrder).HasColumnName("DisplayOrder");
        builder.Property(x => x.IsActive).HasColumnName("IsActive");
    }
}
