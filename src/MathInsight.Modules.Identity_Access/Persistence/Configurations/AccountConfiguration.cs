using MathInsight.Modules.Identity_Access.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Identity_Access.Persistence.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable(nameof(Account));

        builder.HasKey(account => account.AccountId);

        builder.Property(account => account.AccountId)
            .HasColumnName("AccountID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();

        builder.Property(account => account.Username)
            .HasColumnName("Username")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(account => account.PasswordHash)
            .HasColumnName("PasswordHash")
            .HasMaxLength(255)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(account => account.Email)
            .HasColumnName("Email")
            .HasMaxLength(100)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(account => account.FirstName)
            .HasColumnName("FirstName")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(account => account.LastName)
            .HasColumnName("LastName")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(account => account.PhoneNumber)
            .HasColumnName("PhoneNumber")
            .HasMaxLength(20)
            .IsUnicode(false);

        builder.Property(account => account.DateOfBirth)
            .HasColumnName("DateOfBirth")
            .HasColumnType("date");

        builder.Property(account => account.AvatarUrl)
            .HasColumnName("AvatarUrl")
            .HasMaxLength(255)
            .IsUnicode(false);

        builder.Property(account => account.RoleId)
            .HasColumnName("RoleID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(account => account.IsActive)
            .HasColumnName("isActive")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(account => account.CreatedTime)
            .HasColumnName("CreatedTime")
            .HasColumnType("datetime2(0)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(account => account.GoogleSubId)
            .HasColumnName("GoogleSubID")
            .HasMaxLength(255)
            .IsUnicode(false);

        builder.Property(account => account.GoogleEmail)
            .HasColumnName("GoogleEmail")
            .HasMaxLength(100)
            .IsUnicode(false);

        builder.HasIndex(account => account.Username)
            .IsUnique();

        builder.HasIndex(account => account.Email)
            .IsUnique();

        builder.HasIndex(account => account.RoleId);

        builder.HasOne(account => account.Role)
            .WithMany(role => role.Accounts)
            .HasForeignKey(account => account.RoleId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_Account_Role_RoleID");
    }
}

