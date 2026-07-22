using MathInsight.Modules.Testing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Testing.Persistence.Configurations;

public class TestAnswerPartConfiguration : IEntityTypeConfiguration<TestAnswerPart>
{
    public void Configure(EntityTypeBuilder<TestAnswerPart> builder)
    {
        builder.ToTable("TestAnswerPart", table =>
        {
            table.HasCheckConstraint("CK_TestAnswerPart_PointsEarned", "[PointsEarned] >= 0 AND [PointsEarned] <= 10");
        });

        builder.HasKey(x => new { x.TestAnswerId, x.PartId }).HasName("PK_TestAnswerPart");

        builder.Property(x => x.TestAnswerId)
            .HasColumnName("TestAnswerID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(x => x.PartId)
            .HasColumnName("PartID")
            .HasMaxLength(36)
            .IsUnicode(false);

        builder.Property(x => x.BooleanAnswer)
            .HasColumnName("BooleanAnswer");

        builder.Property(x => x.TextAnswer)
            .HasColumnName("TextAnswer")
            .HasMaxLength(255)
            .IsUnicode();

        builder.Property(x => x.NumericAnswer)
            .HasColumnName("NumericAnswer")
            .HasColumnType("decimal(18,6)");

        builder.Property(x => x.IsCorrect)
            .HasColumnName("IsCorrect");

        builder.Property(x => x.PointsEarned)
            .HasColumnName("PointsEarned")
            .HasPrecision(4, 2)
            .HasDefaultValue(0.00m);

        builder.HasOne(x => x.TestAnswer)
            .WithMany(x => x.Parts)
            .HasForeignKey(x => x.TestAnswerId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_TestAnswerPart_TestAnswer_TestAnswerID");

        builder.HasIndex(x => x.PartId)
            .HasDatabaseName("IX_TestAnswerPart_PartID");
    }
}
