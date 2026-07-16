using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Recommender.Persistence.Entities;

namespace MathInsight.Modules.Recommender.Persistence.Configurations;

public sealed class StudentReadOnlyConfiguration : IEntityTypeConfiguration<StudentReadOnly>
{
    public void Configure(EntityTypeBuilder<StudentReadOnly> builder)
    {
        builder.ToTable("Student", table => table.ExcludeFromMigrations());
        builder.HasKey(x => x.StudentId).HasName("PK_Student");

        builder.Property(x => x.StudentId)
            .HasColumnName("StudentID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();

        builder.Property(x => x.CurrentGrade)
            .HasColumnName("CurrentGrade");
    }
}
