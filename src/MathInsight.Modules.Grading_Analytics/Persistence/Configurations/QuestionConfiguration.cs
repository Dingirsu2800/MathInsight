using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;

namespace MathInsight.Modules.Grading_Analytics.Persistence.Configurations;

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("Question", table => table.ExcludeFromMigrations());
        builder.HasKey(x => x.QuestionId);

        builder.Property(x => x.QuestionId).HasColumnName("QuestionID").HasMaxLength(36).IsUnicode(false);
        builder.Property(x => x.QuestionType).HasColumnName("QuestionType").HasMaxLength(30).IsRequired();
        builder.Property(x => x.DefaultWeight).HasColumnName("DefaultWeight").HasPrecision(5, 2);
        builder.Property(x => x.DifficultyId).HasColumnName("DifficultyID").HasMaxLength(36).IsUnicode(false);
        builder.Property(x => x.QuestionContent).HasColumnName("QuestionContent").IsRequired();

        builder.HasMany(x => x.Answers)
               .WithOne(a => a.Question)
               .HasForeignKey(a => a.QuestionId);

        builder.HasMany(x => x.Parts)
               .WithOne(p => p.Question)
               .HasForeignKey(p => p.QuestionId);

        builder.HasMany(x => x.QuestionTopics)
               .WithOne(qt => qt.Question)
               .HasForeignKey(qt => qt.QuestionId);
    }
}
