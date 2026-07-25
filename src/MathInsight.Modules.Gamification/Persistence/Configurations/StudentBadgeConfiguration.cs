using MathInsight.Modules.Gamification.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Gamification.Persistence.Configurations;

/// <summary>
/// Maps <see cref="StudentBadge"/> to DB table [StudentBadge]. Composite PK (StudentID, BadgeID)
/// blocks duplicate awards. Insert-only — never updated or deleted.
/// </summary>
public class StudentBadgeConfiguration : IEntityTypeConfiguration<StudentBadge>
{
    public void Configure(EntityTypeBuilder<StudentBadge> builder)
    {
        builder.ToTable(nameof(StudentBadge));

        builder.HasKey(studentBadge => new { studentBadge.StudentId, studentBadge.BadgeId });

        builder.Property(studentBadge => studentBadge.StudentId)
            .HasColumnName("StudentID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();

        builder.Property(studentBadge => studentBadge.BadgeId)
            .HasColumnName("BadgeID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();

        builder.Property(studentBadge => studentBadge.EarnedTime)
            .HasColumnName("EarnedTime");
    }
}
