using MathInsight.Modules.TestGen.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.TestGen.Persistence.Configurations;

public class TagDifficultyReadModelConfiguration : IEntityTypeConfiguration<TagDifficultyReadModel>
{
    public void Configure(EntityTypeBuilder<TagDifficultyReadModel> builder)
    {
        builder.ToTable("TagDifficulty", table => table.ExcludeFromMigrations());
        builder.HasKey(x => x.DifficultyId).HasName("PK_TagDifficulty");

        builder.Property(x => x.DifficultyId)
            .HasColumnName("DifficultyID")
            .HasMaxLength(36)
            .IsUnicode(false);
        builder.Property(x => x.DifficultyName)
            .HasColumnName("DifficultyName")
            .HasMaxLength(50)
            .IsUnicode()
            .IsRequired();
        builder.Property(x => x.Description)
            .HasColumnName("Description")
            .HasMaxLength(255)
            .IsUnicode();
        builder.Property(x => x.LevelValue)
            .HasColumnName("LevelValue")
            .HasDefaultValue(1);
        builder.Property(x => x.DisplayOrder)
            .HasColumnName("DisplayOrder")
            .HasDefaultValue(1);
        builder.Property(x => x.IsActive)
            .HasColumnName("IsActive")
            .HasDefaultValue(true);
    }
}
