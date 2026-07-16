using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Recommender.Persistence.Entities;

namespace MathInsight.Modules.Recommender.Persistence.Configurations;

public class StudentReadOnlyConfiguration : IEntityTypeConfiguration<StudentReadOnly>
{
    public void Configure(EntityTypeBuilder<StudentReadOnly> builder)
    {
        builder.ToTable("Student");
        builder.HasKey(x => x.StudentId);

        builder.Property(x => x.StudentId)
            .HasColumnName("StudentID")
            .HasMaxLength(36);

        builder.Property(x => x.CurrentGrade)
            .HasColumnName("CurrentGrade");
    }
}
