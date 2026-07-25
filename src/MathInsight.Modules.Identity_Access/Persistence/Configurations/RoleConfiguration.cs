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

        // Seed the four system roles. IDs/names/descriptions match the SQL seed
        // script (database/002_Seed_MathInsight_Demo.sql) so both sources agree.
        builder.HasData(
            new Role { RoleId = "11111111-1111-1111-1111-111111111111", RoleName = "Admin", Description = "System administrator" },
            new Role { RoleId = "22222222-2222-2222-2222-222222222222", RoleName = "Expert", Description = "Question bank expert" },
            new Role { RoleId = "33333333-3333-3333-3333-333333333333", RoleName = "Teacher", Description = "Verified teacher" },
            new Role { RoleId = "44444444-4444-4444-4444-444444444444", RoleName = "Student", Description = "Student user" });
    }
}

