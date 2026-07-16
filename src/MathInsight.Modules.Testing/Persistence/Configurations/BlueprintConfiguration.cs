using MathInsight.Modules.Testing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Testing.Persistence.Configurations;

public class BlueprintConfiguration : IEntityTypeConfiguration<Blueprint>
{
    public void Configure(EntityTypeBuilder<Blueprint> builder)
    {
        builder.ToTable("Blueprint", table => table.ExcludeFromMigrations());
        builder.HasKey(x => x.BlueprintId).HasName("PK_Blueprint");

        builder.Property(x => x.BlueprintId)
            .HasColumnName("BlueprintID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(x => x.BlueprintName)
            .HasColumnName("BlueprintName")
            .HasMaxLength(100)
            .IsUnicode()
            .IsRequired();
    }
}
