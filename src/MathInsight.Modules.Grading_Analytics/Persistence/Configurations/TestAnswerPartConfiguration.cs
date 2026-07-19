using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;

namespace MathInsight.Modules.Grading_Analytics.Persistence.Configurations;

public class TestAnswerPartConfiguration : IEntityTypeConfiguration<TestAnswerPart>
{
    public void Configure(EntityTypeBuilder<TestAnswerPart> builder)
    {
        builder.ToTable("TestAnswerPart");
        
        // Composite PK: (TestAnswerID, PartID)
        builder.HasKey(x => new { x.TestAnswerId, x.PartId });

        builder.Property(x => x.TestAnswerId).HasColumnName("TestAnswerID");
        builder.Property(x => x.PartId).HasColumnName("PartID");
        
        builder.Property(x => x.BooleanAnswer).HasColumnName("BooleanAnswer");
        builder.Property(x => x.TextAnswer).HasColumnName("TextAnswer").HasMaxLength(255);
        builder.Property(x => x.NumericAnswer).HasColumnName("NumericAnswer").HasPrecision(18, 6);
        
        builder.Property(x => x.IsCorrect).HasColumnName("IsCorrect");
        builder.Property(x => x.PointsEarned).HasColumnName("PointsEarned").HasPrecision(4, 2);

        builder.HasOne(x => x.TestAnswer)
               .WithMany(a => a.AnswerParts)
               .HasForeignKey(x => x.TestAnswerId);

        builder.HasOne(x => x.QuestionPart)
               .WithMany()
               .HasForeignKey(x => x.PartId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
