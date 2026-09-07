using MathInsight.Modules.Grading_Analytics.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Grading_Analytics.Persistence.Configurations;

public sealed class QuestionReportIncidentConfiguration : IEntityTypeConfiguration<QuestionReportIncident>
{
    public void Configure(EntityTypeBuilder<QuestionReportIncident> builder)
    {
        builder.ToTable("QuestionReportIncident", table => table.ExcludeFromMigrations());
        builder.HasKey(item => item.IncidentId);
        builder.Property(item => item.IncidentId).HasColumnName("IncidentID").HasMaxLength(36).IsUnicode(false);
        builder.Property(item => item.Status).HasColumnName("Status").HasMaxLength(30).IsUnicode(false);
        builder.Property(item => item.RequiresAdminReview).HasColumnName("RequiresAdminReview");
        builder.Property(item => item.ApprovedResolutionAction).HasColumnName("ApprovedResolutionAction").HasMaxLength(30).IsUnicode(false);
        builder.Property(item => item.AdjustmentStatus).HasColumnName("AdjustmentStatus").HasMaxLength(30).IsUnicode(false);
    }
}
