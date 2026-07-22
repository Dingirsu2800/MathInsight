using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;

namespace MathInsight.Modules.Grading_Analytics.Persistence.Configurations;

public class QuestionPartConfiguration : IEntityTypeConfiguration<QuestionPart>
{
    public void Configure(EntityTypeBuilder<QuestionPart> builder)
    {
        builder.ToTable("QuestionPart", table => table.ExcludeFromMigrations());
        builder.HasKey(x => x.QuestionPartId);

        builder.Property(x => x.QuestionPartId).HasColumnName("PartID").HasMaxLength(36).IsUnicode(false);
        builder.Property(x => x.QuestionId).HasColumnName("QuestionID").HasMaxLength(36).IsUnicode(false);
        builder.Property(x => x.PartOrder).HasColumnName("PartOrder");
        builder.Property(x => x.PartLabel).HasColumnName("PartLabel").HasMaxLength(10);
        builder.Property(x => x.Content).HasColumnName("PartContent").IsRequired();
        builder.Property(x => x.PartType).HasColumnName("PartType").HasMaxLength(30).IsRequired();

        builder.Property(x => x.CorrectBoolean).HasColumnName("CorrectBoolean");
        builder.Property(x => x.CorrectText).HasColumnName("CorrectText").HasMaxLength(255);
        builder.Property(x => x.CorrectNumeric).HasColumnName("CorrectNumeric").HasPrecision(18, 6);
        builder.Property(x => x.NumericTolerance).HasColumnName("NumericTolerance").HasPrecision(18, 6);

        builder.Property(x => x.DefaultWeight).HasColumnName("DefaultWeight").HasPrecision(5, 2);
        builder.Property(x => x.IsArchived).HasColumnName("IsArchived");
        builder.Property(x => x.Explanation).HasColumnName("Explanation");

        builder.HasOne(x => x.Question)
               .WithMany(q => q.Parts)
               .HasForeignKey(x => x.QuestionId);
    }
}
