using MathInsight.Modules.Testing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Testing.Persistence.Configurations;

public class TestAnswerOptionConfiguration : IEntityTypeConfiguration<TestAnswerOption>
{
    public void Configure(EntityTypeBuilder<TestAnswerOption> builder)
    {
        builder.ToTable("TestAnswerOption");

        builder.HasKey(x => new { x.TestAnswerId, x.AnswerId }).HasName("PK_TestAnswerOption");

        builder.Property(x => x.TestAnswerId)
            .HasColumnName("TestAnswerID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(x => x.AnswerId)
            .HasColumnName("AnswerID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.HasOne(x => x.TestAnswer)
            .WithMany(x => x.Options)
            .HasForeignKey(x => x.TestAnswerId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_TestAnswerOption_TestAnswer_TestAnswerID");
    }
}
