using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;

namespace MathInsight.Modules.Grading_Analytics.Persistence.Configurations;

public class TestAnswerConfiguration : IEntityTypeConfiguration<TestAnswer>
{
    public void Configure(EntityTypeBuilder<TestAnswer> builder)
    {
        builder.ToTable("TestAnswer");
        builder.HasKey(x => x.TestAnswerId);

        builder.Property(x => x.TestAnswerId).HasColumnName("TestAnswerID");
        builder.Property(x => x.SessionId).HasColumnName("SessionID");
        builder.Property(x => x.QuestionId).HasColumnName("QuestionID");
        builder.Property(x => x.AnswerId).HasColumnName("AnswerID");
        builder.Property(x => x.QuestionNo).HasColumnName("QuestionNo");
        builder.Property(x => x.TimeSpent).HasColumnName("TimeSpent");
        builder.Property(x => x.FirstChoiceTime).HasColumnName("FirstChoiceTime");
        builder.Property(x => x.UpdateChoiceTime).HasColumnName("UpdateChoiceTime");
        builder.Property(x => x.ShortAnswerText).HasColumnName("ShortAnswerText");
        builder.Property(x => x.IsCorrect).HasColumnName("IsCorrect");
        builder.Property(x => x.PointsEarned).HasColumnName("PointsEarned").HasPrecision(4, 2);

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
