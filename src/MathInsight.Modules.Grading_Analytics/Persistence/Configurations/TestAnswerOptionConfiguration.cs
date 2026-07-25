using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;

namespace MathInsight.Modules.Grading_Analytics.Persistence.Configurations;

public class TestAnswerOptionConfiguration : IEntityTypeConfiguration<TestAnswerOption>
{
    public void Configure(EntityTypeBuilder<TestAnswerOption> builder)
    {
        builder.ToTable("TestAnswerOption", table => table.ExcludeFromMigrations());

        // Composite PK: (test_answer_id, answer_id)
        builder.HasKey(x => new { x.TestAnswerId, x.AnswerId });

        builder.Property(x => x.TestAnswerId).HasColumnName("TestAnswerID").HasMaxLength(36).IsUnicode(false);
        builder.Property(x => x.AnswerId).HasColumnName("AnswerID").HasMaxLength(36).IsUnicode(false);

        builder.HasOne(x => x.TestAnswer)
               .WithMany(a => a.SelectedOptions)
               .HasForeignKey(x => x.TestAnswerId);

        builder.HasOne(x => x.Answer)
               .WithMany()
               .HasForeignKey(x => x.AnswerId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
