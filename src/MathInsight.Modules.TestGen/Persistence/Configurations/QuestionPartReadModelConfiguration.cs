using MathInsight.Modules.TestGen.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.TestGen.Persistence.Configurations;

public sealed class QuestionPartReadModelConfiguration : IEntityTypeConfiguration<QuestionPartReadModel>
{
    public void Configure(EntityTypeBuilder<QuestionPartReadModel> builder)
    {
        builder.ToTable("QuestionPart", table => table.ExcludeFromMigrations());
        builder.HasKey(x => x.PartId).HasName("PK_QuestionPart");
        builder.Property(x => x.PartId).HasColumnName("PartID").HasMaxLength(36).IsUnicode(false);
        builder.Property(x => x.QuestionId).HasColumnName("QuestionID").HasMaxLength(36).IsUnicode(false);
        builder.Property(x => x.PartOrder).HasColumnName("PartOrder");
        builder.Property(x => x.PartType).HasColumnName("PartType").HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.CorrectBoolean).HasColumnName("CorrectBoolean");
        builder.Property(x => x.CorrectText).HasColumnName("CorrectText").HasMaxLength(255).IsUnicode();
        builder.Property(x => x.CorrectNumeric).HasColumnName("CorrectNumeric").HasPrecision(18, 6);
        builder.Property(x => x.NumericTolerance).HasColumnName("NumericTolerance").HasPrecision(18, 6);
        builder.Property(x => x.DefaultWeight).HasColumnName("DefaultWeight").HasPrecision(5, 2);
        builder.Property(x => x.IsArchived).HasColumnName("IsArchived");
        builder.HasIndex(x => new { x.QuestionId, x.PartOrder })
            .IsUnique()
            .HasFilter("[IsArchived] = 0")
            .HasDatabaseName("UX_QuestionPart_Current_Order");
        builder.HasIndex(x => x.QuestionId)
            .HasFilter("[IsArchived] = 0")
            .HasDatabaseName("IX_QuestionPart_Current_Question");
    }
}
