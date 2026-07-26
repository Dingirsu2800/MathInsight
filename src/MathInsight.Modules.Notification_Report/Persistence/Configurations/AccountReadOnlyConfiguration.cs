using MathInsight.Modules.Notification_Report.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Notification_Report.Persistence.Configurations;

public class AccountReadOnlyConfiguration : IEntityTypeConfiguration<AccountReadOnly>
{
    public void Configure(EntityTypeBuilder<AccountReadOnly> builder)
    {
        builder.ToTable("Account", table => table.ExcludeFromMigrations());

        builder.HasKey(x => x.AccountId);

        builder.Property(x => x.AccountId)
            .HasColumnName("AccountID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(x => x.FirstName)
            .HasColumnName("FirstName")
            .HasMaxLength(50);

        builder.Property(x => x.LastName)
            .HasColumnName("LastName")
            .HasMaxLength(50);
    }
}
