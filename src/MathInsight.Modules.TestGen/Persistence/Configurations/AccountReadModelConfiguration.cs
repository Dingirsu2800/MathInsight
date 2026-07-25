using MathInsight.Modules.TestGen.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.TestGen.Persistence.Configurations;

public sealed class AccountReadModelConfiguration : IEntityTypeConfiguration<AccountReadModel>
{
    public void Configure(EntityTypeBuilder<AccountReadModel> builder)
    {
        builder.ToTable("Account", table => table.ExcludeFromMigrations());
        builder.HasKey(account => account.AccountId).HasName("PK_Account");

        builder.Property(account => account.AccountId)
            .HasColumnName("AccountID")
            .HasMaxLength(36)
            .IsUnicode(false);
        builder.Property(account => account.FirstName)
            .HasColumnName("FirstName")
            .HasMaxLength(50)
            .IsUnicode()
            .IsRequired();
        builder.Property(account => account.LastName)
            .HasColumnName("LastName")
            .HasMaxLength(50)
            .IsUnicode()
            .IsRequired();
    }
}
