using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Modules.TestGen.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.TestGen.Persistence.Configurations;

public class BlueprintConfiguration : IEntityTypeConfiguration<Blueprint>
{
    public void Configure(EntityTypeBuilder<Blueprint> builder)
    {
        builder.ToTable("Blueprint", table =>
        {
            table.HasCheckConstraint("CK_Blueprint_Grade", "[Grade] IN (10, 11, 12)");
            table.HasCheckConstraint("CK_Blueprint_TotalQuestions", "[TotalQuestions] >= 0");
            table.HasCheckConstraint("CK_Blueprint_TotalScore", "[TotalScore] > 0 AND [TotalScore] <= 100");
            table.HasCheckConstraint("CK_Blueprint_DurationMinutes", "[DurationMinutes] >= 0");
            table.HasCheckConstraint(
                "CK_Blueprint_Status",
                "[Status] IN ('Draft', 'PendingReview', 'Approved', 'Rejected', 'Active', 'Deactivated')");
        });

        builder.HasKey(x => x.BlueprintId).HasName("PK_Blueprint");

        builder.Property(x => x.BlueprintId)
            .HasColumnName("BlueprintID")
            .HasMaxLength(36)
            .IsUnicode(false);
        builder.Property(x => x.BlueprintName)
            .HasColumnName("BlueprintName")
            .HasMaxLength(100)
            .IsUnicode()
            .IsRequired();
        builder.Property(x => x.Grade)
            .HasColumnName("Grade")
            .HasDefaultValue(10);
        builder.Property(x => x.TotalQuestions)
            .HasColumnName("TotalQuestions")
            .HasDefaultValue(0);
        builder.Property(x => x.TotalScore)
            .HasColumnName("TotalScore")
            .HasPrecision(5, 2)
            .HasDefaultValue(10m);
        builder.Property(x => x.DurationMinutes)
            .HasColumnName("DurationMinutes")
            .HasDefaultValue(0);
        builder.Property(x => x.ExpertId)
            .HasColumnName("ExpertID")
            .HasMaxLength(36)
            .IsUnicode(false);
        builder.Property(x => x.Status)
            .HasColumnName("Status")
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("Draft");
        builder.Property(x => x.ApprovedBy)
            .HasColumnName("ApprovedBy")
            .HasMaxLength(36)
            .IsUnicode(false);
        builder.Property(x => x.ReviewNote)
            .HasColumnName("ReviewNote")
            .IsUnicode();
        builder.Property(x => x.ReviewTime)
            .HasColumnName("ReviewTime")
            .HasColumnType("datetime2(0)");

        builder.HasOne<ExpertReadModel>()
            .WithMany()
            .HasForeignKey(x => x.ExpertId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_Blueprint_Expert_ExpertID");
        builder.HasOne<ExpertReadModel>()
            .WithMany()
            .HasForeignKey(x => x.ApprovedBy)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_Blueprint_Expert_ApprovedBy");

        builder.HasIndex(x => x.ExpertId)
            .HasDatabaseName("IX_Blueprint_ExpertID");
    }
}
