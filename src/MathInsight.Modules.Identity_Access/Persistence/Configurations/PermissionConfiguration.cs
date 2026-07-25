using MathInsight.Modules.Identity_Access.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Identity_Access.Persistence.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable(nameof(Permission));

        builder.HasKey(permission => permission.PermissionId);

        builder.Property(permission => permission.PermissionId)
            .HasColumnName("PermissionID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();

        builder.Property(permission => permission.PermissionKey)
            .HasColumnName("PermissionKey")
            .HasMaxLength(100)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(permission => permission.Description)
            .HasColumnName("Description")
            .HasMaxLength(255);

        builder.HasIndex(permission => permission.PermissionKey)
            .IsUnique();

        // One permission per row of the Permission Matrix (spec.md, from PRD §3.2).
        // The SQL seed script does not populate Permission/RolePermission, so these
        // keys originate here.
        builder.HasData(
            new Permission { PermissionId = "f1000000-0000-0000-0000-000000000001", PermissionKey = "auth:login", Description = "Log in and out of the platform" },
            new Permission { PermissionId = "f1000000-0000-0000-0000-000000000002", PermissionKey = "account:register", Description = "Register a new account" },
            new Permission { PermissionId = "f1000000-0000-0000-0000-000000000003", PermissionKey = "teacher:verify", Description = "Verify teacher credentials" },
            new Permission { PermissionId = "f1000000-0000-0000-0000-000000000004", PermissionKey = "account:deactivate", Description = "Activate or deactivate accounts" },
            new Permission { PermissionId = "f1000000-0000-0000-0000-000000000005", PermissionKey = "account:import", Description = "Import accounts in batch" },
            new Permission { PermissionId = "f1000000-0000-0000-0000-000000000006", PermissionKey = "permission:adjust", Description = "Adjust role permissions" });
    }
}
