using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Modules.TestGen.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.TestGen.Persistence.Configurations;

public class BlueprintDetailConfiguration : IEntityTypeConfiguration<BlueprintDetail>
{
    public void Configure(EntityTypeBuilder<BlueprintDetail> builder)
    {
        builder.ToTable("BlueprintDetail", table =>
        {
            table.HasCheckConstraint("CK_BlueprintDetail_Quantity", "[Quantity] >= 0");
        });

        builder.HasKey(x => x.BlueprintDetailId).HasName("PK_BlueprintDetail");

        builder.Property(x => x.BlueprintDetailId)
            .HasColumnName("BlueprintDetailID")
            .HasMaxLength(36)
            .IsUnicode(false);
        builder.Property(x => x.BlueprintId)
            .HasColumnName("BlueprintID")
            .HasMaxLength(36)
            .IsUnicode(false);
        builder.Property(x => x.BlueprintSectionId)
            .HasColumnName("BlueprintSectionID")
            .HasMaxLength(36)
            .IsUnicode(false);
        builder.Property(x => x.TagId)
            .HasColumnName("TagID")
            .HasMaxLength(36)
            .IsUnicode(false);
        builder.Property(x => x.DifficultyId)
            .HasColumnName("DifficultyID")
            .HasMaxLength(36)
            .IsUnicode(false);
        builder.Property(x => x.Quantity)
            .HasColumnName("Quantity")
            .HasDefaultValue(0);

        builder.HasOne(x => x.BlueprintSection)
            .WithMany(x => x.Details)
            .HasForeignKey(x => new { x.BlueprintSectionId, x.BlueprintId })
            .HasPrincipalKey(x => new { x.BlueprintSectionId, x.BlueprintId })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_BlueprintDetail_BlueprintSection_BlueprintSectionID");
        builder.HasOne<TagTopicReadModel>()
            .WithMany()
            .HasForeignKey(x => x.TagId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_BlueprintDetail_TagTopic_TagID");
        builder.HasOne<TagDifficultyReadModel>()
            .WithMany()
            .HasForeignKey(x => x.DifficultyId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_BlueprintDetail_TagDifficulty_DifficultyID");

        builder.HasIndex(x => new { x.BlueprintSectionId, x.TagId, x.DifficultyId })
            .IsUnique()
            .HasDatabaseName("UQ_BlueprintDetail_Section_Tag_Difficulty");
        builder.HasIndex(x => x.BlueprintSectionId)
            .HasDatabaseName("IX_BlueprintDetail_BlueprintSectionID");
    }
}
