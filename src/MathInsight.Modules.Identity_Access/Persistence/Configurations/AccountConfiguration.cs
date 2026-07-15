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

        // Seed the 5 development accounts from TDS §3.6. IDs, usernames, emails, and the
        // BCrypt hash (password "password") match the SQL seed script
        // (database/002_Seed_MathInsight_Demo.sql). Every seeded account is active —
        // is_active = true is the only valid state for a persisted account (DD-01).
        const string passwordHash = "$2a$11$IgmdnGcpWz7hvryLYzdjQ..DHZIv4jtsRxhDV8qVG7RixntcnJuRa";
        var createdTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        builder.HasData(
            new Account { AccountId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", Username = "admin", PasswordHash = passwordHash, Email = "admin@mathinsight.local", FirstName = "Admin", LastName = "User", RoleId = "11111111-1111-1111-1111-111111111111", IsActive = true, CreatedTime = createdTime },
            new Account { AccountId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", Username = "expert_01", PasswordHash = passwordHash, Email = "expert01@mathinsight.local", FirstName = "Expert", LastName = "One", RoleId = "22222222-2222-2222-2222-222222222222", IsActive = true, CreatedTime = createdTime },
            new Account { AccountId = "cccccccc-cccc-cccc-cccc-cccccccccccc", Username = "teacher_01", PasswordHash = passwordHash, Email = "teacher01@mathinsight.local", FirstName = "Teacher", LastName = "One", RoleId = "33333333-3333-3333-3333-333333333333", IsActive = true, CreatedTime = createdTime },
            new Account { AccountId = "dddddddd-dddd-dddd-dddd-dddddddddddd", Username = "student_01", PasswordHash = passwordHash, Email = "student01@mathinsight.local", FirstName = "Student", LastName = "One", RoleId = "44444444-4444-4444-4444-444444444444", IsActive = true, CreatedTime = createdTime },
            new Account { AccountId = "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", Username = "student_02", PasswordHash = passwordHash, Email = "student02@mathinsight.local", FirstName = "Student", LastName = "Two", RoleId = "44444444-4444-4444-4444-444444444444", IsActive = true, CreatedTime = createdTime });
    }
}

