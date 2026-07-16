using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;

namespace MathInsight.Modules.Grading_Analytics.Persistence.Configurations;

public class AnswerConfiguration : IEntityTypeConfiguration<Answer>
{
    public void Configure(EntityTypeBuilder<Answer> builder)
    {
        builder.ToTable("Answer");
        builder.HasKey(x => x.AnswerId);

        builder.Property(x => x.AnswerId).HasColumnName("AnswerID");
        builder.Property(x => x.QuestionId).HasColumnName("QuestionID");
        builder.Property(x => x.AnswerContent).HasColumnName("AnswerContent").IsRequired();
        builder.Property(x => x.IsCorrect).HasColumnName("IsCorrect");

        builder.HasOne(x => x.Question)
               .WithMany(q => q.Answers)
               .HasForeignKey(x => x.QuestionId);
    }
}
