using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;

namespace MathInsight.Modules.Grading_Analytics.Persistence.Configurations;

public class TestAnswerPartConfiguration : IEntityTypeConfiguration<TestAnswerPart>
{
    public void Configure(EntityTypeBuilder<TestAnswerPart> builder)
    {
        builder.ToTable("TestAnswerPart");
        builder.HasKey(x => x.TestAnswerPartId);

        builder.Property(x => x.TestAnswerPartId).HasColumnName("test_answer_part_id");
        builder.Property(x => x.TestAnswerId).HasColumnName("test_answer_id");
        builder.Property(x => x.QuestionPartId).HasColumnName("question_part_id");
        builder.Property(x => x.StudentAnswer).HasColumnName("student_answer").HasMaxLength(1000);
        builder.Property(x => x.IsCorrect).HasColumnName("is_correct");
        builder.Property(x => x.PointsEarned).HasColumnName("points_earned").HasPrecision(5, 2);

        builder.HasOne(x => x.TestAnswer)
               .WithMany(a => a.AnswerParts)
               .HasForeignKey(x => x.TestAnswerId);

        builder.HasOne(x => x.QuestionPart)
               .WithMany()
               .HasForeignKey(x => x.QuestionPartId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
