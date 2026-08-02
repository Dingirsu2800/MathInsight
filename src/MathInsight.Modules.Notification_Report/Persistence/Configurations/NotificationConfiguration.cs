using MathInsight.Modules.Notification_Report.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Notification_Report.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Notification"/> to DB table [Notification]. UserID is a scalar column only —
/// the DDL declares a physical FK to [Account], but Account belongs to the Identity module, so no
/// EF navigation is modeled here (mirrors ActivityLogConfiguration in Gamification).
/// </summary>
public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable(nameof(Notification));

        builder.HasKey(notification => notification.NotificationId);

        builder.Property(notification => notification.NotificationId)
            .HasColumnName("NotificationID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();

        builder.Property(notification => notification.UserId)
            .HasColumnName("UserID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(notification => notification.Title)
            .HasColumnName("Title")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(notification => notification.Content)
            .HasColumnName("Content")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(notification => notification.Link)
            .HasColumnName("Link")
            .HasMaxLength(255)
            .IsUnicode(false);

        builder.Property(notification => notification.IsRead)
            .HasColumnName("isRead")
            .HasDefaultValue(false);

        builder.Property(notification => notification.CreatedTime)
            .HasColumnName("CreatedTime");

        // BR-22 read pattern: list a user's notifications, optionally filtered to unread.
        builder.HasIndex(notification => new { notification.UserId, notification.IsRead })
            .HasDatabaseName("IX_Notification_User_IsRead");

        // BR-21 prune pattern: delete rows older than 90 days.
        builder.HasIndex(notification => notification.CreatedTime)
            .HasDatabaseName("IX_Notification_CreatedTime");
    }
}
