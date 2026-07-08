using MathInsight.Modules.QuestionBank.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.QuestionBank.Persistence.Configurations;

public class AnswerConfiguration : IEntityTypeConfiguration<Answer>
{
    public void Configure(EntityTypeBuilder<Answer> builder)
    {
        builder.ToTable(nameof(Answer));

        builder.HasKey(answer => answer.AnswerId)
            .HasName("PK_Answer");

        builder.Property(answer => answer.AnswerId)
            .HasColumnName("AnswerID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();

        builder.Property(answer => answer.QuestionId)
            .HasColumnName("QuestionID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(answer => answer.AnswerContent)
            .HasColumnName("AnswerContent")
            .IsRequired();

        builder.Property(answer => answer.IsCorrect)
            .HasColumnName("IsCorrect")
            .HasDefaultValue(false)
            .IsRequired();

        builder.HasIndex(answer => answer.QuestionId)
            .HasDatabaseName("IX_Answer_QuestionID");

        builder.HasOne(answer => answer.Question)
            .WithMany(question => question.Answers)
            .HasForeignKey(answer => answer.QuestionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_Answer_Question_QuestionID");
    }
}
