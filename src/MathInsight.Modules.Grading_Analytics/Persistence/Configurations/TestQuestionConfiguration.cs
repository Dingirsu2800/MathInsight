using MathInsight.Modules.Grading_Analytics.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Grading_Analytics.Persistence.Configurations;

public sealed class TestQuestionConfiguration : IEntityTypeConfiguration<TestQuestion>
{
    public void Configure(EntityTypeBuilder<TestQuestion> builder)
    {
        builder.ToTable("TestQuestion", table => table.ExcludeFromMigrations());
        builder.HasKey(x => new { x.TestId, x.QuestionId });
        builder.Property(x => x.TestId).HasColumnName("TestID").HasMaxLength(36).IsUnicode(false);
        builder.Property(x => x.QuestionId).HasColumnName("QuestionID").HasMaxLength(36).IsUnicode(false);
        builder.Property(x => x.QuestionVersionId).HasColumnName("QuestionVersionID").HasMaxLength(36).IsUnicode(false);
        builder.Property(x => x.WeightSnapshot).HasColumnName("WeightSnapshot").HasPrecision(5, 2);
        builder.Property(x => x.MaxPointsSnapshot).HasColumnName("MaxPointsSnapshot").HasPrecision(5, 2);
        builder.Property(x => x.ScoringRuleSnapshot).HasColumnName("ScoringRuleSnapshot").HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.IsScoreInvalidated).HasColumnName("IsScoreInvalidated");
        builder.Property(x => x.InvalidatedByReportId).HasColumnName("InvalidatedByReportID").HasMaxLength(36).IsUnicode(false);
        builder.HasOne(x => x.QuestionVersion).WithMany().HasForeignKey(x => x.QuestionVersionId).OnDelete(DeleteBehavior.NoAction);
    }
}
