using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;

namespace MathInsight.Modules.Grading_Analytics.Persistence.Configurations;

public class QuestionPartConfiguration : IEntityTypeConfiguration<QuestionPart>
{
    public void Configure(EntityTypeBuilder<QuestionPart> builder)
    {
        builder.ToTable("QuestionParts");
        builder.HasKey(x => x.QuestionPartId);

        builder.Property(x => x.QuestionPartId).HasColumnName("question_part_id");
        builder.Property(x => x.QuestionId).HasColumnName("question_id");
        builder.Property(x => x.PartOrder).HasColumnName("part_order");
        builder.Property(x => x.Content).HasColumnName("content").HasMaxLength(2000).IsRequired();
        builder.Property(x => x.AnswerKey).HasColumnName("answer_key").HasMaxLength(500).IsRequired();
        builder.Property(x => x.PointValue).HasColumnName("point_value").HasPrecision(5, 2);
        builder.Property(x => x.Explanation).HasColumnName("explanation").HasMaxLength(2000);
        builder.Property(x => x.PartType).HasColumnName("part_type").HasMaxLength(50).IsRequired();

        builder.HasOne(x => x.Question)
               .WithMany(q => q.Parts)
               .HasForeignKey(x => x.QuestionId);
    }
}
