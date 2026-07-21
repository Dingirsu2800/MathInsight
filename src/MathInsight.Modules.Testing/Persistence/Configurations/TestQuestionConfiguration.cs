using MathInsight.Modules.Testing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Testing.Persistence.Configurations;

public class TestQuestionConfiguration : IEntityTypeConfiguration<TestQuestion>
{
    public void Configure(EntityTypeBuilder<TestQuestion> builder)
    {
        builder.ToTable("TestQuestion", table =>
        {
            table.ExcludeFromMigrations();
            table.HasCheckConstraint("CK_TestQuestion_QuestionOrder", "[QuestionOrder] > 0");
            table.HasCheckConstraint("CK_TestQuestion_PtagAtSelection", "[PtagAtSelection] IS NULL OR ([PtagAtSelection] >= 0 AND [PtagAtSelection] <= 10)");
            table.HasCheckConstraint("CK_TestQuestion_SelectionReason", "[SelectionReason] IN ('BlueprintNormal', 'WeakTagPractice', 'RemedialPractice', 'ChallengeMode', 'Exploration', 'TopicPractice', 'Diagnostic')");
        });

        builder.HasKey(x => new { x.TestId, x.QuestionId }).HasName("PK_TestQuestion");

        builder.Property(x => x.TestId)
            .HasColumnName("TestID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(x => x.QuestionId)
            .HasColumnName("QuestionID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(x => x.QuestionOrder)
            .HasColumnName("QuestionOrder")
            .HasDefaultValue(1);

        builder.Property(x => x.SourceBlueprintDetailId)
            .HasColumnName("SourceBlueprintDetailID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(x => x.SelectionReason)
            .HasColumnName("SelectionReason")
            .HasMaxLength(40)
            .IsUnicode(false)
            .HasDefaultValue("BlueprintNormal");

        builder.Property(x => x.IsAdaptiveSelected)
            .HasColumnName("IsAdaptiveSelected")
            .HasDefaultValue(false);

        builder.Property(x => x.RecommendedForTagId)
            .HasColumnName("RecommendedForTagID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(x => x.RecommendedDifficultyId)
            .HasColumnName("RecommendedDifficultyID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(x => x.PtagAtSelection)
            .HasColumnName("PtagAtSelection")
            .HasColumnType("decimal(5,2)");

        builder.Property(x => x.RuleVersion)
            .HasColumnName("RuleVersion")
            .HasMaxLength(30)
            .IsUnicode(false);

        builder.HasOne(x => x.Test)
            .WithMany(x => x.Questions)
            .HasForeignKey(x => x.TestId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_TestQuestion_Test_TestID");

        builder.HasIndex(x => new { x.TestId, x.QuestionOrder })
            .IsUnique()
            .HasDatabaseName("UQ_TestQuestion_Test_Order");

        builder.HasIndex(x => x.SelectionReason)
            .HasDatabaseName("IX_TestQuestion_SelectionReason");

        builder.HasIndex(x => new { x.RecommendedForTagId, x.RecommendedDifficultyId })
            .HasFilter("[RecommendedForTagID] IS NOT NULL")
            .HasDatabaseName("IX_TestQuestion_RecommendedTag_Difficulty");

        builder.HasIndex(x => x.SourceBlueprintDetailId)
            .HasFilter("[SourceBlueprintDetailID] IS NOT NULL")
            .HasDatabaseName("IX_TestQuestion_SourceBlueprintDetailID");
    }
}
