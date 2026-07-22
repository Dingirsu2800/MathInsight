using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;

namespace MathInsight.Modules.Grading_Analytics.Persistence.Configurations;

public class QuestionPartConfiguration : IEntityTypeConfiguration<QuestionPart>
{
    public void Configure(EntityTypeBuilder<QuestionPart> builder)
    {
        builder.ToTable("QuestionPart");
        builder.HasKey(x => x.QuestionPartId);

        builder.Property(x => x.QuestionPartId).HasColumnName("PartID");
        builder.Property(x => x.QuestionId).HasColumnName("QuestionID");
        builder.Property(x => x.PartOrder).HasColumnName("PartOrder");
        builder.Property(x => x.PartLabel).HasColumnName("PartLabel").HasMaxLength(10);
        builder.Property(x => x.Content).HasColumnName("PartContent").IsRequired();
        builder.Property(x => x.PartType).HasColumnName("PartType").HasMaxLength(30).IsRequired();
        
        builder.Property(x => x.CorrectBoolean).HasColumnName("CorrectBoolean");
        builder.Property(x => x.CorrectText).HasColumnName("CorrectText").HasMaxLength(255);
        builder.Property(x => x.CorrectNumeric).HasColumnName("CorrectNumeric").HasPrecision(18, 6);
        builder.Property(x => x.NumericTolerance).HasColumnName("NumericTolerance").HasPrecision(18, 6);
        
        builder.Property(x => x.DefaultWeight).HasColumnName("DefaultWeight").HasPrecision(4, 2);
        builder.Property(x => x.Explanation).HasColumnName("Explanation");

        builder.HasOne(x => x.Question)
               .WithMany(q => q.Parts)
               .HasForeignKey(x => x.QuestionId);
    }
}
