using MathInsight.Modules.Identity_Access.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Identity_Access.Persistence.Configurations;

public class TeacherConfiguration : IEntityTypeConfiguration<Teacher>
{
    public void Configure(EntityTypeBuilder<Teacher> builder)
    {
        builder.ToTable(nameof(Teacher));

        builder.HasKey(teacher => teacher.TeacherId);

        builder.Property(teacher => teacher.TeacherId)
           .HasColumnName("TeacherID")
           .HasMaxLength(36)
           .IsUnicode(false)
           .ValueGeneratedNever();

        builder.Property(teacher => teacher.Biography)
            .HasColumnName("Biography");

        builder.Property(teacher => teacher.IsVerified)
            .HasColumnName("isVerified")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(teacher => teacher.CccdNumber)
            .HasColumnName("cccd_number")
            .HasMaxLength(12)
            .IsUnicode(false);

        builder.HasOne(teacher => teacher.Account)
            .WithOne(account => account.Teacher)
            .HasForeignKey<Teacher>(teacher => teacher.TeacherId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_Teacher_Account_TeacherID");
    }
}

