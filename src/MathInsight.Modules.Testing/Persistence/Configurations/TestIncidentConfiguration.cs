using MathInsight.Modules.Testing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Testing.Persistence.Configurations;

public class TestIncidentConfiguration : IEntityTypeConfiguration<TestIncident>
{
    public void Configure(EntityTypeBuilder<TestIncident> builder)
    {
        builder.ToTable("TestIncidents");

        builder.HasKey(x => x.IncidentId).HasName("PK_TestIncidents");

        builder.Property(x => x.IncidentId)
            .HasColumnName("IncidentID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(x => x.SessionId)
            .HasColumnName("SessionID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(x => x.Type)
            .HasColumnName("Type")
            .HasMaxLength(50)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Time)
            .HasColumnName("Time")
            .HasColumnType("datetime2(0)")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(x => x.Session)
            .WithMany(x => x.Incidents)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_TestIncidents_TestSession_SessionID");
    }
}
