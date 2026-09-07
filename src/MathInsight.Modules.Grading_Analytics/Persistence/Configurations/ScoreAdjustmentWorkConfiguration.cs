using MathInsight.Modules.Grading_Analytics.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Grading_Analytics.Persistence.Configurations;

public sealed class ScoreAdjustmentWorkConfiguration : IEntityTypeConfiguration<ScoreAdjustmentWork>
{
    public void Configure(EntityTypeBuilder<ScoreAdjustmentWork> builder)
    {
        builder.ToTable("ScoreAdjustmentWork", table => table.ExcludeFromMigrations());
        builder.HasKey(item => item.WorkId);

        builder.Property(item => item.WorkId).HasColumnName("WorkID").HasMaxLength(36).IsUnicode(false);
        builder.Property(item => item.ReportId).HasColumnName("ReportID").HasMaxLength(36).IsUnicode(false).IsRequired();
        builder.Property(item => item.IncidentId).HasColumnName("IncidentID").HasMaxLength(36).IsUnicode(false);
        builder.Property(item => item.SessionId).HasColumnName("SessionID").HasMaxLength(36).IsUnicode(false).IsRequired();
        builder.Property(item => item.GradeRevision).HasColumnName("GradeRevision").IsRequired();
        builder.Property(item => item.EventPayload).HasColumnName("EventPayload").IsRequired();
        builder.Property(item => item.Status).HasColumnName("Status").HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(item => item.AttemptCount).HasColumnName("AttemptCount").IsRequired();
        builder.Property(item => item.LastError).HasColumnName("LastError").HasMaxLength(2000);
        builder.Property(item => item.CreatedTime).HasColumnName("CreatedTime").HasColumnType("datetime2(0)").IsRequired();
        builder.Property(item => item.CompletedTime).HasColumnName("CompletedTime").HasColumnType("datetime2(0)");

        builder.HasIndex(item => new { item.IncidentId, item.SessionId, item.GradeRevision })
            .IsUnique()
            .HasFilter("[IncidentID] IS NOT NULL")
            .HasDatabaseName("UQ_ScoreAdjustmentWork_Incident_Session_Revision");
        builder.HasIndex(item => new { item.ReportId, item.SessionId, item.GradeRevision })
            .IsUnique()
            .HasFilter("[IncidentID] IS NULL")
            .HasDatabaseName("UQ_ScoreAdjustmentWork_LegacyReport_Session_Revision");
        builder.HasIndex(item => new { item.Status, item.CreatedTime })
            .HasDatabaseName("IX_ScoreAdjustmentWork_Status_CreatedTime");
    }
}
