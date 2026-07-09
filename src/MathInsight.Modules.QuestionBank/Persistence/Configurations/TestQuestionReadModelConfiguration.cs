using MathInsight.Modules.QuestionBank.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.QuestionBank.Persistence.Configurations;

public class TestQuestionReadModelConfiguration : IEntityTypeConfiguration<TestQuestionReadModel>
{
    public void Configure(EntityTypeBuilder<TestQuestionReadModel> builder)
    {
        builder.ToTable("TestQuestion", table => table.ExcludeFromMigrations());

        builder.HasKey(testQuestion => new { testQuestion.TestId, testQuestion.QuestionId });

        builder.Property(testQuestion => testQuestion.TestId)
            .HasColumnName("TestID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(testQuestion => testQuestion.QuestionId)
            .HasColumnName("QuestionID")
            .HasMaxLength(36)
            .IsUnicode(false);
    }
}
