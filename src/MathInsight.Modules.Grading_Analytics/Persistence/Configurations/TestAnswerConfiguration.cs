using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;

namespace MathInsight.Modules.Grading_Analytics.Persistence.Configurations;

public class TestAnswerConfiguration : IEntityTypeConfiguration<TestAnswer>
{
    public void Configure(EntityTypeBuilder<TestAnswer> builder)
    {
        builder.ToTable("TestAnswers");
        builder.HasKey(x => x.TestAnswerId);

        builder.Property(x => x.TestAnswerId).HasColumnName("test_answer_id");
        builder.Property(x => x.SessionId).HasColumnName("session_id");
        builder.Property(x => x.QuestionId).HasColumnName("question_id");
        builder.Property(x => x.AnswerId).HasColumnName("answer_id");
        builder.Property(x => x.QuestionNo).HasColumnName("question_no");
        builder.Property(x => x.TimeSpent).HasColumnName("time_spent");
        builder.Property(x => x.FirstChoiceTime).HasColumnName("first_choice_time");
        builder.Property(x => x.UpdateChoiceTime).HasColumnName("update_choice_time");
        builder.Property(x => x.ShortAnswerText).HasColumnName("short_answer_text").HasMaxLength(500);
        builder.Property(x => x.IsCorrect).HasColumnName("is_correct");
        builder.Property(x => x.PointsEarned).HasColumnName("points_earned").HasPrecision(5, 2);

        builder.HasOne(x => x.Session)
               .WithMany(s => s.TestAnswers)
               .HasForeignKey(x => x.SessionId);

        builder.HasOne(x => x.Question)
               .WithMany()
               .HasForeignKey(x => x.QuestionId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.SelectedOptions)
               .WithOne(o => o.TestAnswer)
               .HasForeignKey(o => o.TestAnswerId);

        builder.HasMany(x => x.AnswerParts)
               .WithOne(p => p.TestAnswer)
               .HasForeignKey(p => p.TestAnswerId);
    }
}
