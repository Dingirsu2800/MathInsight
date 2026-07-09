using MathInsight.Modules.QuestionBank.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.QuestionBank.Persistence.Configurations;

public class AccountReadModelConfiguration : IEntityTypeConfiguration<AccountReadModel>
{
    public void Configure(EntityTypeBuilder<AccountReadModel> builder)
    {
        builder.ToTable("Account", table => table.ExcludeFromMigrations());

        builder.HasKey(account => account.AccountId);

        builder.Property(account => account.AccountId)
            .HasColumnName("AccountID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(account => account.Username)
            .HasColumnName("Username")
            .HasMaxLength(50);

        builder.Property(account => account.Email)
            .HasColumnName("Email")
            .HasMaxLength(100)
            .IsUnicode(false);

        builder.Property(account => account.FirstName)
            .HasColumnName("FirstName")
            .HasMaxLength(50);

        builder.Property(account => account.LastName)
            .HasColumnName("LastName")
            .HasMaxLength(50);
    }
}
