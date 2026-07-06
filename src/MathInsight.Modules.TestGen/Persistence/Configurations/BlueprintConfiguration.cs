using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.TestGen.Persistence.Entities;

namespace MathInsight.Modules.TestGen.Persistence.Configurations;

public class BlueprintConfiguration : IEntityTypeConfiguration<Blueprint>
{
    public void Configure(EntityTypeBuilder<Blueprint> builder)
    {
        builder.ToTable("Blueprint");
        builder.HasKey(x => x.BlueprintId);

        builder.Property(x => x.BlueprintId)
            .HasColumnName("blueprint_id");

        builder.Property(x => x.BlueprintName)
            .HasColumnName("blueprint_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Grade)
            .HasColumnName("grade")
            .IsRequired();

        builder.Property(x => x.TotalQuestions)
            .HasColumnName("total_questions")
            .IsRequired();

        builder.Property(x => x.DurationMinutes)
            .HasColumnName("duration_minutes")
            .IsRequired();

        builder.Property(x => x.ExpertId)
            .HasColumnName("expert_id")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.ReviewNote)
            .HasColumnName("review_note")
            .HasMaxLength(255);

        builder.Property(x => x.ReviewedBy)
            .HasColumnName("reviewed_by");

        builder.Property(x => x.ReviewedTime)
            .HasColumnName("reviewed_time");

        builder.Property(x => x.CreatedTime)
            .HasColumnName("created_time")
            .IsRequired();

        // Check Constraints
        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_Blueprint_Status",
                "[status] IN ('DRAFT', 'PENDING_REVIEW', 'APPROVED', 'REJECTED', 'ACTIVE')");
            t.HasCheckConstraint(
                "CK_Blueprint_Grade",
                "[grade] IN (10, 11, 12)");
        });
    }
}
