using MathInsight.Modules.TestGen.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.TestGen.Persistence.Configurations;

public class TestConfiguration : IEntityTypeConfiguration<Test>
{
    public void Configure(EntityTypeBuilder<Test> builder)
    {
        builder.ToTable("Test", table =>
        {
            table.HasCheckConstraint("CK_Test_Status", "[TestStatus] IN ('Active', 'Archived')");
            table.HasCheckConstraint(
                "CK_Test_Mode",
                "[TestMode] IN ('BlueprintExam', 'AdaptivePractice', 'TopicPractice', 'Diagnostic')");
            table.HasCheckConstraint("CK_Test_GeneratedBy", "[GeneratedBy] IN ('Expert', 'System')");
            table.HasCheckConstraint(
                "CK_Test_Blueprint_Required",
                "[TestMode] <> 'BlueprintExam' OR [BlueprintID] IS NOT NULL");
            table.HasCheckConstraint("CK_Test_DurationMinutes", "[DurationMinutes] > 0");
            table.HasCheckConstraint("CK_Test_TotalQuestions", "[TotalQuestions] > 0");
            table.HasCheckConstraint("CK_Test_MaxScore", "[MaxScore] > 0 AND [MaxScore] <= 100");
            table.HasCheckConstraint("CK_Test_ScoringPolicy", "[ScoringPolicy] IN ('BlueprintBudget', 'NormalizedWeight')");
        });

        builder.HasKey(x => x.TestId).HasName("PK_Test");

        builder.Property(x => x.TestId)
            .HasColumnName("TestID")
            .HasMaxLength(36)
            .IsUnicode(false);
        builder.Property(x => x.BlueprintId)
            .HasColumnName("BlueprintID")
            .HasMaxLength(36)
            .IsUnicode(false);
        builder.Property(x => x.TestStatus)
            .HasColumnName("TestStatus")
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("Active");
        builder.Property(x => x.TestMode)
            .HasColumnName("TestMode")
            .HasMaxLength(30)
            .IsUnicode(false)
            .HasDefaultValue("BlueprintExam");
        builder.Property(x => x.GeneratedForStudentId)
            .HasColumnName("GeneratedForStudentID")
            .HasMaxLength(36)
            .IsUnicode(false);
        builder.Property(x => x.GeneratedBy)
            .HasColumnName("GeneratedBy")
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("System");
        builder.Property(x => x.TestName)
            .HasColumnName("TestName")
            .HasMaxLength(100)
            .IsUnicode()
            .IsRequired();
        builder.Property(x => x.TestCode)
            .HasColumnName("TestCode")
            .HasMaxLength(20)
            .IsUnicode(false);
        builder.Property(x => x.DurationMinutes)
            .HasColumnName("DurationMinutes");
        builder.Property(x => x.TotalQuestions)
            .HasColumnName("TotalQuestions");
        builder.Property(x => x.MaxScore)
            .HasColumnName("MaxScore")
            .HasPrecision(5, 2);
        builder.Property(x => x.ScoringPolicy)
            .HasColumnName("ScoringPolicy")
            .HasMaxLength(30)
            .IsUnicode(false);
        builder.Property(x => x.CreatedTime)
            .HasColumnName("CreatedTime")
            .HasColumnType("datetime2(0)")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(x => x.Blueprint)
            .WithMany(x => x.Tests)
            .HasForeignKey(x => x.BlueprintId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_Test_Blueprint_BlueprintID");
        builder.HasOne(x => x.GeneratedForStudent)
            .WithMany(x => x.GeneratedTests)
            .HasForeignKey(x => x.GeneratedForStudentId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_Test_Student_GeneratedForStudentID");

        builder.HasIndex(x => x.TestCode)
            .IsUnique()
            .HasFilter("[TestCode] IS NOT NULL")
            .HasDatabaseName("UX_Test_TestCode_NotNull");
        builder.HasIndex(x => x.BlueprintId)
            .HasDatabaseName("IX_Test_BlueprintID");
        builder.HasIndex(x => new { x.TestMode, x.GeneratedForStudentId })
            .HasDatabaseName("IX_Test_Mode_GeneratedForStudent");
        builder.HasIndex(x => new { x.GeneratedForStudentId, x.CreatedTime })
            .HasFilter("[GeneratedForStudentID] IS NOT NULL")
            .HasDatabaseName("IX_Test_GeneratedForStudent_CreatedTime");
    }
}
