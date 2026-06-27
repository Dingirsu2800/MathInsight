using MathInsight.Modules.Identity_Access.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Identity_Access.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Role");

        builder.HasKey(role => role.RoleId);

        builder.Property(role => role.RoleId)
            .HasColumnName("RoleID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();

        builder.Property(role => role.RoleName)
            .HasColumnName("RoleName")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(role => role.Description)
            .HasColumnName("Description")
            .HasMaxLength(255);

        builder.HasIndex(role => role.RoleName)
            .IsUnique();
    }
}

