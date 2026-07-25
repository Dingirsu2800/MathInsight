using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Recommender.Persistence.Entities;

namespace MathInsight.Modules.Recommender.Persistence.Configurations;

/// <summary>
/// EF configuration for CompetencyPoint.
/// Maps to DB script table: CompetencyPoint (no schema prefix — uses shared connection).
/// Unique constraint: (student_id, grade). Point range: 0.00..10.00.
/// </summary>
public class CompetencyPointConfiguration : IEntityTypeConfiguration<CompetencyPoint>
{
    public void Configure(EntityTypeBuilder<CompetencyPoint> builder)
    {
        builder.ToTable("CompetencyPoint");
        builder.HasKey(x => x.CompetencyId);

        builder.Property(x => x.CompetencyId)
            .HasColumnName("CompetencyID")
            .HasMaxLength(36);

        builder.Property(x => x.StudentId)
            .HasColumnName("StudentID")
            .HasMaxLength(36)
            .IsRequired();

        builder.Property(x => x.Grade)
            .HasColumnName("Grade")
            .IsRequired();

        builder.Property(x => x.Point)
            .HasColumnName("Point")
            .HasPrecision(5, 2)
            .IsRequired();

        // Unique (StudentID, Grade)
        builder.HasIndex(x => new { x.StudentId, x.Grade })
            .IsUnique()
            .HasDatabaseName("UQ_CompetencyPoint_Student_Grade");

        // Enforce range via check constraint
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_CompetencyPoint_Point_Range",
            "[Point] >= 0.00 AND [Point] <= 10.00"));
    }
}
