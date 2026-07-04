using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;

namespace MathInsight.Modules.Grading_Analytics.Persistence.Configurations;

public class AnswerConfiguration : IEntityTypeConfiguration<Answer>
{
    public void Configure(EntityTypeBuilder<Answer> builder)
    {
        builder.ToTable("Answers");
        builder.HasKey(x => x.AnswerId);

        builder.Property(x => x.AnswerId).HasColumnName("answer_id");
        builder.Property(x => x.QuestionId).HasColumnName("question_id");
        builder.Property(x => x.AnswerContent).HasColumnName("answer_content").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.IsCorrect).HasColumnName("is_correct");

        builder.HasOne(x => x.Question)
               .WithMany(q => q.Answers)
               .HasForeignKey(x => x.QuestionId);
    }
}
