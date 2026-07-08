using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.TestGen.Persistence.Entities;

namespace MathInsight.Modules.TestGen.Persistence.Configurations;

public class TestConfiguration : IEntityTypeConfiguration<Test>
{
    public void Configure(EntityTypeBuilder<Test> builder)
    {
        builder.ToTable("Test");
        builder.HasKey(x => x.TestId);

        builder.Property(x => x.TestId)
            .HasColumnName("test_id");

        builder.Property(x => x.BlueprintId)
            .HasColumnName("blueprint_id");

        builder.Property(x => x.TestFormat)
            .HasColumnName("test_format")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.GeneratedForStudentId)
            .HasColumnName("generated_for_student_id");

        builder.Property(x => x.GeneratedBy)
            .HasColumnName("generated_by")
            .HasMaxLength(50)
            .HasDefaultValue("System")
            .IsRequired();

        builder.Property(x => x.TestName)
            .HasColumnName("test_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.TestCode)
            .HasColumnName("test_code")
            .HasMaxLength(50);

        builder.Property(x => x.DurationMinutes)
            .HasColumnName("duration_minutes")
            .IsRequired();

        builder.Property(x => x.TotalQuestions)
            .HasColumnName("total_questions")
            .IsRequired();

        builder.Property(x => x.TestStatus)
            .HasColumnName("test_status")
            .HasMaxLength(20)
            .HasDefaultValue("ACTIVE")
            .IsRequired();

        builder.Property(x => x.CreatedTime)
            .HasColumnName("created_time")
            .IsRequired();

        // Unique filtered index on TestCode (since it is nullable but unique when populated)
        builder.HasIndex(x => x.TestCode)
            .IsUnique()
            .HasFilter("[test_code] IS NOT NULL")
            .HasDatabaseName("UQ_Test_TestCode");

        // Check constraints
        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_Test_TestFormat",
                "[test_format] IN ('Practice', 'Exam')");
            t.HasCheckConstraint(
                "CK_Test_TestStatus",
                "[test_status] IN ('ACTIVE', 'ARCHIVED')");
        });
    }
}
