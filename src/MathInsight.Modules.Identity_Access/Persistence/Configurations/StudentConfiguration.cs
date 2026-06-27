using MathInsight.Modules.Identity_Access.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Identity_Access.Persistence.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable(nameof(Student));

        builder.HasKey(student => student.StudentId);

        builder.Property(student => student.StudentId)
            .HasColumnName("StudentID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();

        builder.Property(student => student.Gender)
            .HasColumnName("Gender")
            .HasMaxLength(10)
            .IsUnicode(false);

        builder.Property(student => student.School)
            .HasColumnName("School")
            .HasMaxLength(100);

        builder.Property(student => student.CurrentGrade)
            .HasColumnName("CurrentGrade");

        builder.HasOne(student => student.Account)
            .WithOne(account => account.Student)
            .HasForeignKey<Student>(student => student.StudentId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_Student_Account_StudentID");
    }
}

