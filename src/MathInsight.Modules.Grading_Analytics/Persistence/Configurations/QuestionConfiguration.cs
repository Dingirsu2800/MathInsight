using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;

namespace MathInsight.Modules.Grading_Analytics.Persistence.Configurations;

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("Question");
        builder.HasKey(x => x.QuestionId);

        builder.Property(x => x.QuestionId).HasColumnName("QuestionID");
        builder.Property(x => x.QuestionType).HasColumnName("QuestionType").HasMaxLength(30).IsRequired();
        builder.Property(x => x.DefaultPoint).HasColumnName("DefaultPoint").HasPrecision(4, 2);
        builder.Property(x => x.DifficultyLevel).HasColumnName("DifficultyLevel");
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
