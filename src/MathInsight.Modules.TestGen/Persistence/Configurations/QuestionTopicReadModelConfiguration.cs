using MathInsight.Modules.TestGen.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.TestGen.Persistence.Configurations;

public sealed class QuestionTopicReadModelConfiguration : IEntityTypeConfiguration<QuestionTopicReadModel>
{
    public void Configure(EntityTypeBuilder<QuestionTopicReadModel> builder)
    {
        builder.ToTable("QuestionTopic", table => table.ExcludeFromMigrations());
        builder.HasKey(x => x.QuestionTopicId).HasName("PK_QuestionTopic");

        builder.Property(x => x.QuestionTopicId)
            .HasColumnName("QuestionTopicID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();
        builder.Property(x => x.QuestionId)
            .HasColumnName("QuestionID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(x => x.TagId)
            .HasColumnName("TagID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(x => x.IsPrimary)
            .HasColumnName("IsPrimary");

        builder.HasIndex(x => new { x.QuestionId, x.TagId })
            .IsUnique()
            .HasDatabaseName("UQ_QuestionTopic_Question_Tag");

        builder.HasOne(x => x.Question)
            .WithMany(x => x.Topics)
            .HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_QuestionTopic_Question_QuestionID");
    }
}
