using MathInsight.Modules.QuestionBank.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.QuestionBank.Persistence.Configurations;

public class QuestionPartConfiguration : IEntityTypeConfiguration<QuestionPart>
{
    public void Configure(EntityTypeBuilder<QuestionPart> builder)
    {
        builder.ToTable(nameof(QuestionPart));

        builder.HasKey(part => part.PartId)
            .HasName("PK_QuestionPart");

        builder.Property(part => part.PartId)
            .HasColumnName("PartID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();

        builder.Property(part => part.QuestionId)
            .HasColumnName("QuestionID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(part => part.PartOrder)
            .HasColumnName("PartOrder")
            .IsRequired();

        builder.Property(part => part.PartLabel)
            .HasColumnName("PartLabel")
            .HasMaxLength(10);

        builder.Property(part => part.PartContent)
            .HasColumnName("PartContent")
            .IsRequired();

        builder.Property(part => part.PartType)
            .HasColumnName("PartType")
            .HasMaxLength(30)
            .IsUnicode(false)
            .HasDefaultValue("TrueFalse")
            .IsRequired();

        builder.Property(part => part.CorrectBoolean)
            .HasColumnName("CorrectBoolean");

        builder.Property(part => part.CorrectText)
            .HasColumnName("CorrectText")
            .HasMaxLength(255);

        builder.Property(part => part.CorrectNumeric)
            .HasColumnName("CorrectNumeric")
            .HasColumnType("decimal(18,6)");

        builder.Property(part => part.NumericTolerance)
            .HasColumnName("NumericTolerance")
            .HasColumnType("decimal(18,6)");

        builder.Property(part => part.Explanation)
            .HasColumnName("Explanation");

        builder.Property(part => part.DefaultPoint)
            .HasColumnName("DefaultPoint")
            .HasColumnType("decimal(4,2)")
            .HasDefaultValue(0.00m)
            .IsRequired();

        builder.HasIndex(part => part.QuestionId)
            .HasDatabaseName("IX_QuestionPart_QuestionID");

        builder.HasIndex(part => new { part.QuestionId, part.PartOrder })
            .IsUnique()
            .HasDatabaseName("UQ_QuestionPart_Question_Order");

        builder.HasIndex(part => new { part.QuestionId, part.PartLabel })
            .IsUnique()
            .HasFilter("[PartLabel] IS NOT NULL")
            .HasDatabaseName("UX_QuestionPart_Label_NotNull");

        builder.HasOne(part => part.Question)
            .WithMany(question => question.Parts)
            .HasForeignKey(part => part.QuestionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_QuestionPart_Question_QuestionID");
    }
}
