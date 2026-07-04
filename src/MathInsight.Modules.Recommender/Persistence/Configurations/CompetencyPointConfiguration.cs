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
            .HasColumnName("competency_id");

        builder.Property(x => x.StudentId)
            .HasColumnName("student_id")
            .IsRequired();

        builder.Property(x => x.Grade)
            .HasColumnName("grade")
            .IsRequired();

        builder.Property(x => x.Point)
            .HasColumnName("point")
            .HasPrecision(4, 2)
            .IsRequired();

        // Unique (student_id, grade)
        builder.HasIndex(x => new { x.StudentId, x.Grade })
            .IsUnique()
            .HasDatabaseName("UQ_CompetencyPoint_Student_Grade");

        // Enforce range via check constraint
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_CompetencyPoint_Point_Range",
            "[point] >= 0.00 AND [point] <= 10.00"));
    }
}
