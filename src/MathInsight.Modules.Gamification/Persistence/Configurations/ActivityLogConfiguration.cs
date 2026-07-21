using MathInsight.Modules.Gamification.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Gamification.Persistence.Configurations;

/// <summary>
/// Maps <see cref="ActivityLog"/> to DB table [ActivityLog]. Insert-only (BR-40).
/// StudentID / TestSessionID / LectureID / MaterialID are scalar columns only — the DDL
/// declares no FK constraints and those aggregates belong to other modules.
/// </summary>
public class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.ToTable(nameof(ActivityLog));

        builder.HasKey(activityLog => activityLog.ActivityLogId);

        builder.Property(activityLog => activityLog.ActivityLogId)
            .HasColumnName("ActivityLogID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();

        builder.Property(activityLog => activityLog.StudentId)
            .HasColumnName("StudentID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .IsRequired();

        // VARCHAR(50), persisted by enum name.
        builder.Property(activityLog => activityLog.ActivityType)
            .HasColumnName("ActivityType")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(activityLog => activityLog.TestSessionId)
            .HasColumnName("TestSessionID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(activityLog => activityLog.LectureId)
            .HasColumnName("LectureID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(activityLog => activityLog.MaterialId)
            .HasColumnName("MaterialID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(activityLog => activityLog.DurationSeconds)
            .HasColumnName("DurationSeconds");

        builder.Property(activityLog => activityLog.ActivityDate)
            .HasColumnName("ActivityDate");

        // Non-unique lookup index for per-student activity-by-date queries (streak/badge reads).
        builder.HasIndex(activityLog => new { activityLog.StudentId, activityLog.ActivityDate })
            .HasDatabaseName("IX_ActivityLog_Student_Date");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_ActivityLog_DurationSeconds",
                "[DurationSeconds] IS NULL OR [DurationSeconds] >= 0");
        });
    }
}
