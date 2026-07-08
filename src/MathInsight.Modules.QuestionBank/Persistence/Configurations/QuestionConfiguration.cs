using MathInsight.Modules.QuestionBank.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.QuestionBank.Persistence.Configurations;

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable(nameof(Question));

        builder.HasKey(question => question.QuestionId)
            .HasName("PK_Question");

        builder.Property(question => question.QuestionId)
            .HasColumnName("QuestionID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();

        builder.Property(question => question.QuestionContent)
            .HasColumnName("QuestionContent")
            .IsRequired();

        builder.Property(question => question.SolutionContent)
            .HasColumnName("SolutionContent")
            .IsRequired();

        builder.Property(question => question.PictureUrl)
            .HasColumnName("PictureUrl")
            .HasMaxLength(255)
            .IsUnicode(false);

        builder.Property(question => question.DifficultyId)
            .HasColumnName("DifficultyID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(question => question.Grade)
            .HasColumnName("Grade")
            .HasDefaultValue(10)
            .IsRequired();

        builder.Property(question => question.Status)
            .HasColumnName("Status")
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("Approved")
            .IsRequired();

        builder.Property(question => question.QuestionType)
            .HasColumnName("QuestionType")
            .HasMaxLength(30)
            .IsUnicode(false)
            .HasDefaultValue("SingleChoice")
            .IsRequired();

        builder.Property(question => question.ExpertId)
            .HasColumnName("ExpertID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(question => question.DefaultPoint)
            .HasColumnName("DefaultPoint")
            .HasColumnType("decimal(4,2)")
            .HasDefaultValue(0.20m)
            .IsRequired();

        builder.Property(question => question.IsActive)
            .HasColumnName("IsActive")
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(question => new { question.Status, question.IsActive })
            .HasDatabaseName("IX_Question_Status_IsActive");

        builder.HasIndex(question => question.ExpertId)
            .HasDatabaseName("IX_Question_ExpertID");

        builder.HasOne(question => question.Difficulty)
            .WithMany(difficulty => difficulty.Questions)
            .HasForeignKey(question => question.DifficultyId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_Question_TagDifficulty_DifficultyID");
    }
}
