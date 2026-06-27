using MathInsight.Modules.Identity_Access.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Identity_Access.Persistence.Configurations
{
    public class ExpertConfiguration : IEntityTypeConfiguration<Expert>
    {
        public void Configure(EntityTypeBuilder<Expert> builder)
        {
            builder.ToTable(nameof(Expert));

            builder.HasKey(expert => expert.ExpertId);

            builder.Property(expert => expert.ExpertId)
            .HasColumnName("ExpertID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();

            builder.Property(expert => expert.Specialty)
                .HasColumnName("Specialty")
                .HasMaxLength(100)
                .IsUnicode(false);

            builder.HasOne(expert => expert.Account)
                .WithOne(account => account.Expert)
                .HasForeignKey<Expert>(expert => expert.ExpertId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_Expert_Account_ExpertID");
        }
    }
}
