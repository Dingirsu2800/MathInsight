using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.TestGen.Persistence.Entities;

namespace MathInsight.Modules.TestGen.Persistence.Configurations;

public class BlueprintDetailConfiguration : IEntityTypeConfiguration<BlueprintDetail>
{
    public void Configure(EntityTypeBuilder<BlueprintDetail> builder)
    {
        builder.ToTable("BlueprintDetail");
        builder.HasKey(x => x.BlueprintDetailId);

        builder.Property(x => x.BlueprintDetailId)
            .HasColumnName("blueprint_detail_id");

        builder.Property(x => x.BlueprintId)
            .HasColumnName("blueprint_id")
            .IsRequired();

        builder.Property(x => x.BlueprintSectionId)
            .HasColumnName("blueprint_section_id")
            .IsRequired();

        builder.Property(x => x.TagId)
            .HasColumnName("tag_id")
            .IsRequired();

        builder.Property(x => x.DifficultyId)
            .HasColumnName("difficulty_id")
            .IsRequired();

        builder.Property(x => x.Quantity)
            .HasColumnName("quantity")
            .IsRequired();

        // Relationships
        builder.HasOne(x => x.BlueprintSection)
            .WithMany(x => x.Details)
            .HasForeignKey(x => x.BlueprintSectionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Composite UNIQUE: (blueprint_section_id, tag_id, difficulty_id)
        builder.HasIndex(x => new { x.BlueprintSectionId, x.TagId, x.DifficultyId })
            .IsUnique()
            .HasDatabaseName("UQ_BlueprintDetail_Section_Tag_Difficulty");

        // Check Constraint: quantity >= 1
        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_BlueprintDetail_Quantity",
                "[quantity] >= 1");
        });
    }
}
