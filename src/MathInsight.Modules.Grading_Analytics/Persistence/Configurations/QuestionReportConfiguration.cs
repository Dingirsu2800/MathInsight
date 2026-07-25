using MathInsight.Modules.Grading_Analytics.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Grading_Analytics.Persistence.Configurations;

public sealed class QuestionReportConfiguration : IEntityTypeConfiguration<QuestionReport>
{
    public void Configure(EntityTypeBuilder<QuestionReport> builder)
    {
        builder.ToTable("QuestionReport", table => table.ExcludeFromMigrations());
        builder.HasKey(item => item.ReportId);
        builder.Property(item => item.ReportId).HasColumnName("ReportID").HasMaxLength(36).IsUnicode(false);
        builder.Property(item => item.QuestionId).HasColumnName("QuestionID").HasMaxLength(36).IsUnicode(false);
        builder.Property(item => item.ReporterRole).HasColumnName("ReporterRole").HasMaxLength(20).IsUnicode(false);
        builder.Property(item => item.ReportReason).HasColumnName("ReportReason");
        builder.Property(item => item.Status).HasColumnName("Status").HasMaxLength(20).IsUnicode(false);
        builder.Property(item => item.SessionId).HasColumnName("SessionID").HasMaxLength(36).IsUnicode(false);
        builder.Property(item => item.QuestionVersionId).HasColumnName("QuestionVersionID").HasMaxLength(36).IsUnicode(false);
        builder.Property(item => item.ResolutionAction).HasColumnName("ResolutionAction").HasMaxLength(30).IsUnicode(false);
        builder.Property(item => item.ScoreAdjustedTime).HasColumnName("ScoreAdjustedTime");
    }
}
