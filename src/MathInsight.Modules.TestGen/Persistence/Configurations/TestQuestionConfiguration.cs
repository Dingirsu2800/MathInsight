using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.TestGen.Persistence.Entities;

namespace MathInsight.Modules.TestGen.Persistence.Configurations;

public class TestQuestionConfiguration : IEntityTypeConfiguration<TestQuestion>
{
    public void Configure(EntityTypeBuilder<TestQuestion> builder)
    {
        builder.ToTable("TestQuestion");
        builder.HasKey(x => new { x.TestId, x.QuestionId });

        builder.Property(x => x.TestId)
            .HasColumnName("test_id");

        builder.Property(x => x.QuestionId)
            .HasColumnName("question_id");

        builder.Property(x => x.QuestionOrder)
            .HasColumnName("question_order")
            .IsRequired();
    }
}
