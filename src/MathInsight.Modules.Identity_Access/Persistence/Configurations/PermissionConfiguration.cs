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
    }
}
