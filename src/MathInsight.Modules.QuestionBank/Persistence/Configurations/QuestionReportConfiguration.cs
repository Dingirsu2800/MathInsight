using MathInsight.Modules.QuestionBank.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.QuestionBank.Persistence.Configurations;

public class QuestionReportConfiguration : IEntityTypeConfiguration<QuestionReport>
{
    public void Configure(EntityTypeBuilder<QuestionReport> builder)
    {
        builder.ToTable(nameof(QuestionReport));

        builder.HasKey(report => report.ReportId)
            .HasName("PK_QuestionReport");

        builder.Property(report => report.ReportId)
            .HasColumnName("ReportID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();

        builder.Property(report => report.QuestionId)
            .HasColumnName("QuestionID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(report => report.ReporterAccountId)
            .HasColumnName("ReporterAccountID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(report => report.ReporterRole)
            .HasColumnName("ReporterRole")
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(report => report.ReportReason)
            .HasColumnName("ReportReason")
            .IsRequired();

        builder.Property(report => report.Status)
            .HasColumnName("Status")
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("Pending")
            .IsRequired();

        builder.Property(report => report.CreatedTime)
            .HasColumnName("CreatedTime")
            .HasColumnType("datetime2(0)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(report => report.ResolvedTime)
            .HasColumnName("ResolvedTime")
            .HasColumnType("datetime2(0)");

        builder.Property(report => report.ResolvedBy)
            .HasColumnName("ResolvedBy")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(report => report.ReviewNote)
            .HasColumnName("ReviewNote")
            .HasMaxLength(2000);

        builder.Property(report => report.SubmittedTime)
            .HasColumnName("SubmittedTime")
            .HasColumnType("datetime2(0)");

        builder.Property(report => report.ReviewedTime)
            .HasColumnName("ReviewedTime")
            .HasColumnType("datetime2(0)");

        builder.Property(report => report.ReviewedBy)
            .HasColumnName("ReviewedBy")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.HasIndex(report => new { report.QuestionId, report.Status })
            .HasDatabaseName("IX_QuestionReport_Question_Status");

        builder.HasIndex(report => report.ReporterAccountId)
            .HasDatabaseName("IX_QuestionReport_ReporterAccountID");

        builder.HasOne(report => report.Question)
            .WithMany(question => question.Reports)
            .HasForeignKey(report => report.QuestionId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_QuestionReport_Question_QuestionID");
    }
}
