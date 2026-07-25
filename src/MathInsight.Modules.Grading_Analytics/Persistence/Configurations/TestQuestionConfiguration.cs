using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;

namespace MathInsight.Modules.Grading_Analytics.Persistence.Configurations;

public class TestQuestionConfiguration : IEntityTypeConfiguration<TestQuestion>
{
    public void Configure(EntityTypeBuilder<TestQuestion> builder)
    {
        builder.ToTable("TestQuestion");

        // Composite PK: (TestID, QuestionID)
        builder.HasKey(x => new { x.TestId, x.QuestionId });

        builder.Property(x => x.TestId).HasColumnName("TestID");
        builder.Property(x => x.QuestionId).HasColumnName("QuestionID");
        builder.Property(x => x.QuestionVersionId).HasColumnName("QuestionVersionID");
        builder.Property(x => x.WeightSnapshot).HasColumnName("WeightSnapshot").HasPrecision(4, 2);
        builder.Property(x => x.MaxPointsSnapshot).HasColumnName("MaxPointsSnapshot").HasPrecision(5, 2);
        builder.Property(x => x.ScoringRuleSnapshot).HasColumnName("ScoringRuleSnapshot").HasMaxLength(30);
        builder.Property(x => x.IsScoreInvalidated).HasColumnName("IsScoreInvalidated");
        builder.Property(x => x.InvalidatedByReportId).HasColumnName("InvalidatedByReportID");

        builder.HasOne(x => x.Question)
               .WithMany()
               .HasForeignKey(x => x.QuestionId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
