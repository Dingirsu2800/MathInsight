using MathInsight.Modules.Identity_Access.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Identity_Access.Persistence.Configurations;

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable(nameof(RolePermission));

        builder.HasKey(rolePermission => new
        {
            rolePermission.RoleId,
            rolePermission.PermissionId
        });

        builder.Property(rolePermission => rolePermission.RoleId)
            .HasColumnName("RoleID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(rolePermission => rolePermission.PermissionId)
            .HasColumnName("PermissionID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .IsRequired();

        builder.HasOne(rolePermission => rolePermission.Role)
            .WithMany(role => role.RolePermissions)
            .HasForeignKey(rolePermission => rolePermission.RoleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_RolePermission_Role_RoleID");

        builder.HasOne(rolePermission => rolePermission.Permission)
            .WithMany(permission => permission.RolePermissions)
            .HasForeignKey(rolePermission => rolePermission.PermissionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_RolePermission_Permission_PermissionID");

        // Default role → permission grants, read straight from the Permission Matrix
        // (spec.md). A "Full" cell becomes a row here. Guest is not a persisted role,
        // so its column is not seeded. Role IDs match RoleConfiguration; permission IDs
        // match PermissionConfiguration.
        const string admin = "11111111-1111-1111-1111-111111111111";
        const string expert = "22222222-2222-2222-2222-222222222222";
        const string teacher = "33333333-3333-3333-3333-333333333333";
        const string student = "44444444-4444-4444-4444-444444444444";

        const string login = "f1000000-0000-0000-0000-000000000001";
        const string register = "f1000000-0000-0000-0000-000000000002";
        const string verifyTeacher = "f1000000-0000-0000-0000-000000000003";
        const string deactivate = "f1000000-0000-0000-0000-000000000004";
        const string import = "f1000000-0000-0000-0000-000000000005";
        const string adjustPermission = "f1000000-0000-0000-0000-000000000006";

        builder.HasData(
            // Admin: everything except self-registration.
            new RolePermission { RoleId = admin, PermissionId = login },
            new RolePermission { RoleId = admin, PermissionId = verifyTeacher },
            new RolePermission { RoleId = admin, PermissionId = deactivate },
            new RolePermission { RoleId = admin, PermissionId = import },
            new RolePermission { RoleId = admin, PermissionId = adjustPermission },
            // Expert: login/logout only.
            new RolePermission { RoleId = expert, PermissionId = login },
            // Teacher: login/logout and register.
            new RolePermission { RoleId = teacher, PermissionId = login },
            new RolePermission { RoleId = teacher, PermissionId = register },
            // Student: login/logout and register.
            new RolePermission { RoleId = student, PermissionId = login },
            new RolePermission { RoleId = student, PermissionId = register });
    }
}

