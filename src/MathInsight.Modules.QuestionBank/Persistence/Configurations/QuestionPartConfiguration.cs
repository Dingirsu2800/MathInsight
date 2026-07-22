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

        builder.Property(part => part.DefaultWeight)
            .HasColumnName("DefaultWeight")
            .HasColumnType("decimal(5,2)")
            .HasDefaultValue(1.00m)
            .IsRequired();

        builder.Property(part => part.IsArchived)
            .HasColumnName("IsArchived")
            .HasDefaultValue(false)
            .IsRequired();

        builder.HasIndex(part => part.QuestionId)
            .HasFilter("[IsArchived] = 0")
            .HasDatabaseName("IX_QuestionPart_Current_Question");

        builder.HasIndex(part => new { part.QuestionId, part.PartOrder })
            .IsUnique()
            .HasFilter("[IsArchived] = 0")
            .HasDatabaseName("UX_QuestionPart_Current_Order");

        builder.HasIndex(part => new { part.QuestionId, part.PartLabel })
            .IsUnique()
            .HasFilter("[PartLabel] IS NOT NULL AND [IsArchived] = 0")
            .HasDatabaseName("UX_QuestionPart_Current_Label_NotNull");

        builder.HasOne(part => part.Question)
            .WithMany(question => question.Parts)
            .HasForeignKey(part => part.QuestionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_QuestionPart_Question_QuestionID");
    }
}
