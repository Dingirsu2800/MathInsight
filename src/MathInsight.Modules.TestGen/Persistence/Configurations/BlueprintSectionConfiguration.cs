using MathInsight.Modules.TestGen.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.TestGen.Persistence.Configurations;

public class BlueprintSectionConfiguration : IEntityTypeConfiguration<BlueprintSection>
{
    public void Configure(EntityTypeBuilder<BlueprintSection> builder)
    {
        builder.ToTable("BlueprintSection", table =>
        {
            table.HasCheckConstraint("CK_BlueprintSection_Order", "[SectionOrder] > 0");
            table.HasCheckConstraint(
                "CK_BlueprintSection_QuestionType",
                "[QuestionType] IN ('SingleChoice', 'MultipleChoice', 'TrueFalse', 'ShortAnswer', 'Composite')");
            table.HasCheckConstraint("CK_BlueprintSection_TotalQuestions", "[TotalQuestions] >= 0");
            table.HasCheckConstraint(
                "CK_BlueprintSection_DefaultPointPerQuestion",
                "[DefaultPointPerQuestion] >= 0 AND [DefaultPointPerQuestion] <= 10");
            table.HasCheckConstraint(
                "CK_BlueprintSection_DefaultPointPerPart",
                "[DefaultPointPerPart] IS NULL OR ([DefaultPointPerPart] >= 0 AND [DefaultPointPerPart] <= 10)");
            table.HasCheckConstraint(
                "CK_BlueprintSection_PartCountPerQuestion",
                "[PartCountPerQuestion] IS NULL OR [PartCountPerQuestion] > 0");
            table.HasCheckConstraint(
                "CK_BlueprintSection_CompositePartMetadata",
                "([QuestionType] = 'Composite' AND [PartCountPerQuestion] IS NOT NULL AND [DefaultPointPerPart] IS NOT NULL) OR " +
                "([QuestionType] <> 'Composite' AND [PartCountPerQuestion] IS NULL AND [DefaultPointPerPart] IS NULL)");
        });

        builder.HasKey(x => x.BlueprintSectionId).HasName("PK_BlueprintSection");
        builder.HasAlternateKey(x => new { x.BlueprintSectionId, x.BlueprintId })
            .HasName("UQ_BlueprintSection_ID_Blueprint");

        builder.Property(x => x.BlueprintSectionId)
            .HasColumnName("BlueprintSectionID")
            .HasMaxLength(36)
            .IsUnicode(false);
        builder.Property(x => x.BlueprintId)
            .HasColumnName("BlueprintID")
            .HasMaxLength(36)
            .IsUnicode(false);
        builder.Property(x => x.SectionOrder)
            .HasColumnName("SectionOrder")
            .HasDefaultValue(1);
        builder.Property(x => x.SectionCode)
            .HasColumnName("SectionCode")
            .HasMaxLength(20)
            .IsUnicode();
        builder.Property(x => x.SectionName)
            .HasColumnName("SectionName")
            .HasMaxLength(100)
            .IsUnicode()
            .IsRequired();
        builder.Property(x => x.QuestionType)
            .HasColumnName("QuestionType")
            .HasMaxLength(30)
            .IsUnicode(false)
            .HasDefaultValue("SingleChoice");
        builder.Property(x => x.InstructionText)
            .HasColumnName("InstructionText")
            .IsUnicode();
        builder.Property(x => x.TotalQuestions)
            .HasColumnName("TotalQuestions")
            .HasDefaultValue(0);
        builder.Property(x => x.DefaultPointPerQuestion)
            .HasColumnName("DefaultPointPerQuestion")
            .HasPrecision(4, 2)
            .HasDefaultValue(0m);
        builder.Property(x => x.DefaultPointPerPart)
            .HasColumnName("DefaultPointPerPart")
            .HasPrecision(4, 2);
        builder.Property(x => x.PartCountPerQuestion)
            .HasColumnName("PartCountPerQuestion");

        builder.HasOne(x => x.Blueprint)
            .WithMany(x => x.Sections)
            .HasForeignKey(x => x.BlueprintId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_BlueprintSection_Blueprint_BlueprintID");

        builder.HasIndex(x => new { x.BlueprintId, x.SectionOrder })
            .IsUnique()
            .HasDatabaseName("UQ_BlueprintSection_Blueprint_Order");
        builder.HasIndex(x => x.BlueprintId)
            .HasDatabaseName("IX_BlueprintSection_BlueprintID");
    }
}
