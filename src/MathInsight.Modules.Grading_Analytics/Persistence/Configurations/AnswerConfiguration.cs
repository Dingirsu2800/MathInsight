using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;

namespace MathInsight.Modules.Grading_Analytics.Persistence.Configurations;

public class AnswerConfiguration : IEntityTypeConfiguration<Answer>
{
    public void Configure(EntityTypeBuilder<Answer> builder)
    {
        builder.ToTable("Answer", table => table.ExcludeFromMigrations());
        builder.HasKey(x => x.AnswerId);

        builder.Property(x => x.AnswerId).HasColumnName("AnswerID").HasMaxLength(36).IsUnicode(false);
        builder.Property(x => x.QuestionId).HasColumnName("QuestionID").HasMaxLength(36).IsUnicode(false);
        builder.Property(x => x.AnswerContent).HasColumnName("AnswerContent").IsRequired();
        builder.Property(x => x.IsCorrect).HasColumnName("IsCorrect");
        builder.Property(x => x.IsArchived).HasColumnName("IsArchived");

        builder.HasOne(x => x.Question)
               .WithMany(q => q.Answers)
               .HasForeignKey(x => x.QuestionId);
    }
}
