using MathInsight.Modules.Identity_Access.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Identity_Access.Persistence.Configurations
{
    public class TeacherApplicationConfiguration : IEntityTypeConfiguration<TeacherApplication>
    {
        public void Configure(EntityTypeBuilder<TeacherApplication> builder)
        {
            builder.ToTable(nameof(TeacherApplication));

            builder.HasKey(application => application.ApplicationId);

            builder.Property(application => application.ApplicationId)
            .HasColumnName("ApplicationID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();

            builder.Property(application => application.TeacherId)
                .HasColumnName("TeacherID")
                .HasMaxLength(36)
                .IsUnicode(false)
                .IsRequired();

            builder.Property(application => application.DocumentsUrl)
                .HasColumnName("DocumentsUrl")
                .HasMaxLength(255)
                .IsUnicode(false)
                .IsRequired();

            builder.Property(application => application.Status)
                .HasColumnName("Status")
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Pending")
                .IsRequired();

            builder.Property(application => application.ReviewComments)
                .HasColumnName("ReviewComments")
                .HasMaxLength(255);

            builder.Property(application => application.AppliedTime)
                .HasColumnName("AppliedTime")
                .HasColumnType("datetime2(0)")
                .HasDefaultValueSql("SYSUTCDATETIME()")
                .IsRequired();

            builder.Property(application => application.ReviewedTime)
                .HasColumnName("ReviewedTime")
                .HasColumnType("datetime2(0)");

            builder.Property(application => application.ReviewedBy)
                .HasColumnName("ReviewedBy")
                .HasMaxLength(36)
                .IsUnicode(false);

            builder.HasOne(application => application.Teacher)
                .WithMany(teacher => teacher.TeacherApplications)
                .HasForeignKey(application => application.TeacherId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_TeacherApplication_Teacher_TeacherID");

            builder.HasOne(application => application.Reviewer)
                .WithMany()
                .HasForeignKey(application => application.ReviewedBy)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_TeacherApplication_Account_ReviewedBy");

        }
    }
}
