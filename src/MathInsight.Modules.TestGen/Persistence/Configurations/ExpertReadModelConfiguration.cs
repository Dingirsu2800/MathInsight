using MathInsight.Modules.TestGen.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.TestGen.Persistence.Configurations;

public class ExpertReadModelConfiguration : IEntityTypeConfiguration<ExpertReadModel>
{
    public void Configure(EntityTypeBuilder<ExpertReadModel> builder)
    {
        builder.ToTable("Expert", table => table.ExcludeFromMigrations());
        builder.HasKey(x => x.ExpertId).HasName("PK_Expert");

        builder.Property(x => x.ExpertId)
            .HasColumnName("ExpertID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(x => x.Specialty)
            .HasColumnName("Specialty")
            .HasMaxLength(100)
            .IsUnicode(false);
    }
}
