using MathInsight.Modules.Testing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Testing.Persistence.Configurations;

public class TestAnswerConfiguration : IEntityTypeConfiguration<TestAnswer>
{
    public void Configure(EntityTypeBuilder<TestAnswer> builder)
    {
        builder.ToTable("TestAnswer", table =>
        {
            table.HasCheckConstraint("CK_TestAnswer_QuestionNo", "[QuestionNo] > 0");
            table.HasCheckConstraint("CK_TestAnswer_TimeSpent", "[TimeSpent] IS NULL OR [TimeSpent] >= 0");
            table.HasCheckConstraint("CK_TestAnswer_PointsEarned", "[PointsEarned] >= 0 AND [PointsEarned] <= 10");
        });

        builder.HasKey(x => x.TestAnswerId).HasName("PK_TestAnswer");

        builder.Property(x => x.TestAnswerId)
            .HasColumnName("TestAnswerID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(x => x.SessionId)
            .HasColumnName("SessionID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(x => x.QuestionId)
            .HasColumnName("QuestionID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(x => x.AnswerId)
            .HasColumnName("AnswerID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(x => x.QuestionNo)
            .HasColumnName("QuestionNo");

        builder.Property(x => x.TimeSpent)
            .HasColumnName("TimeSpent")
            .HasDefaultValue(0);

        builder.Property(x => x.FirstChoiceTime)
            .HasColumnName("FirstChoiceTime")
            .HasColumnType("datetime2(0)");

        builder.Property(x => x.UpdateChoiceTime)
            .HasColumnName("UpdateChoiceTime")
            .HasColumnType("datetime2(0)");

        builder.Property(x => x.ShortAnswerText)
            .HasColumnName("ShortAnswerText");

        builder.Property(x => x.IsCorrect)
            .HasColumnName("IsCorrect");

        builder.Property(x => x.PointsEarned)
            .HasColumnName("PointsEarned")
            .HasPrecision(4, 2)
            .HasDefaultValue(0.00m);

        builder.HasOne(x => x.Session)
            .WithMany(x => x.Answers)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_TestAnswer_TestSession_SessionID");

        builder.HasIndex(x => new { x.SessionId, x.QuestionId })
            .IsUnique()
            .HasDatabaseName("UQ_TestAnswer_Session_Question");
    }
}
