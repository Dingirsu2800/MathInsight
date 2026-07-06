using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.TestGen.Persistence.Entities;

namespace MathInsight.Modules.TestGen.Persistence.Configurations;

public class BlueprintSectionConfiguration : IEntityTypeConfiguration<BlueprintSection>
{
    public void Configure(EntityTypeBuilder<BlueprintSection> builder)
    {
        builder.ToTable("BlueprintSection");
        builder.HasKey(x => x.BlueprintSectionId);

        builder.Property(x => x.BlueprintSectionId)
            .HasColumnName("blueprint_section_id");

        builder.Property(x => x.BlueprintId)
            .HasColumnName("blueprint_id")
            .IsRequired();

        builder.Property(x => x.SectionOrder)
            .HasColumnName("section_order")
            .IsRequired();

        builder.Property(x => x.SectionCode)
            .HasColumnName("section_code")
            .HasMaxLength(20);

        builder.Property(x => x.SectionName)
            .HasColumnName("section_name")
            .HasMaxLength(100);

        builder.Property(x => x.QuestionType)
            .HasColumnName("question_type")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.InstructionText)
            .HasColumnName("instruction_text")
            .HasMaxLength(500);

        builder.Property(x => x.TotalQuestions)
            .HasColumnName("total_questions")
            .IsRequired();

        builder.Property(x => x.DefaultPointPerQuestion)
            .HasColumnName("default_point_per_question")
            .HasPrecision(5, 2);

        builder.Property(x => x.DefaultPointPerPart)
            .HasColumnName("default_point_per_part")
            .HasPrecision(5, 2);

        builder.Property(x => x.PartCountPerQuestion)
            .HasColumnName("part_count_per_question");

        // Relationships
        builder.HasOne(x => x.Blueprint)
            .WithMany(x => x.Sections)
            .HasForeignKey(x => x.BlueprintId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique constraint (blueprint_id, section_order)
        builder.HasIndex(x => new { x.BlueprintId, x.SectionOrder })
            .IsUnique()
            .HasDatabaseName("UQ_BlueprintSection_Blueprint_Order");

        // Check constraints
        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_BlueprintSection_QuestionType",
                "[question_type] IN ('SingleChoice', 'MultipleChoice', 'TrueFalse', 'ShortAnswer', 'Composite')");

            // Composite sections metadata constraint (BR-51)
            t.HasCheckConstraint(
                "CK_BlueprintSection_Composite_Metadata",
                "([question_type] = 'Composite' AND [part_count_per_question] IS NOT NULL AND [default_point_per_part] IS NOT NULL) OR " +
                "([question_type] <> 'Composite' AND [part_count_per_question] IS NULL AND [default_point_per_part] IS NULL)");
        });
    }
}
