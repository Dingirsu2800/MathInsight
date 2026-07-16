using MathInsight.Modules.TestGen.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.TestGen.Persistence.Configurations;

public sealed class StudentReadModelConfiguration : IEntityTypeConfiguration<StudentReadModel>
{
    public void Configure(EntityTypeBuilder<StudentReadModel> builder)
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
