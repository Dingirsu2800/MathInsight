using MathInsight.Modules.Notification_Report.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Notification_Report.Persistence.Configurations;

public class CompetencyPointReadOnlyConfiguration : IEntityTypeConfiguration<CompetencyPointReadOnly>
{
    public void Configure(EntityTypeBuilder<CompetencyPointReadOnly> builder)
    {
        builder.ToTable("CompetencyPoint", table => table.ExcludeFromMigrations());

        builder.HasKey(x => x.CompetencyId);

        builder.Property(x => x.CompetencyId)
            .HasColumnName("CompetencyID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(x => x.StudentId)
            .HasColumnName("StudentID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(x => x.Grade)
            .HasColumnName("Grade");

        builder.Property(x => x.Point)
            .HasColumnName("Point")
            .HasPrecision(5, 2);
    }
}
