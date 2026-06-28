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
    }
}

