using MathInsight.Modules.QuestionBank.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.QuestionBank.Persistence.Configurations;

public class QuestionTopicConfiguration : IEntityTypeConfiguration<QuestionTopic>
{
    public void Configure(EntityTypeBuilder<QuestionTopic> builder)
    {
        builder.ToTable(nameof(QuestionTopic));

        builder.HasKey(questionTopic => questionTopic.QuestionTopicId)
            .HasName("PK_QuestionTopic");

        builder.Property(questionTopic => questionTopic.QuestionTopicId)
            .HasColumnName("QuestionTopicID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();

        builder.Property(questionTopic => questionTopic.QuestionId)
            .HasColumnName("QuestionID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(questionTopic => questionTopic.TagId)
            .HasColumnName("TagID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(questionTopic => questionTopic.IsPrimary)
            .HasColumnName("IsPrimary")
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(questionTopic => new { questionTopic.QuestionId, questionTopic.TagId })
            .IsUnique()
            .HasDatabaseName("UQ_QuestionTopic_Question_Tag");

        builder.HasIndex(questionTopic => questionTopic.QuestionId)
            .IsUnique()
            .HasFilter("[IsPrimary] = 1")
            .HasDatabaseName("UX_QuestionTopic_OnePrimaryPerQuestion");

        builder.HasOne(questionTopic => questionTopic.Question)
            .WithMany(question => question.QuestionTopics)
            .HasForeignKey(questionTopic => questionTopic.QuestionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_QuestionTopic_Question_QuestionID");

        builder.HasOne(questionTopic => questionTopic.Tag)
            .WithMany(topic => topic.QuestionTopics)
            .HasForeignKey(questionTopic => questionTopic.TagId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_QuestionTopic_TagTopic_TagID");
    }
}
