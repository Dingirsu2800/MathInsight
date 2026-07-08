using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;

namespace MathInsight.Modules.Grading_Analytics.Persistence.Configurations;

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("Questions");
        builder.HasKey(x => x.QuestionId);

        builder.Property(x => x.QuestionId).HasColumnName("question_id");
        builder.Property(x => x.QuestionType).HasColumnName("question_type").HasMaxLength(30).IsRequired();
        builder.Property(x => x.DefaultPoint).HasColumnName("default_point").HasPrecision(5, 2);
        builder.Property(x => x.DifficultyLevel).HasColumnName("difficulty_level");

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
