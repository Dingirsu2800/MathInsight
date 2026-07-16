using MathInsight.Modules.TestGen.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.TestGen.Persistence.Configurations;

public sealed class QuestionReadModelConfiguration : IEntityTypeConfiguration<QuestionReadModel>
{
    public void Configure(EntityTypeBuilder<QuestionReadModel> builder)
    {
        builder.ToTable("Question", table => table.ExcludeFromMigrations());
        builder.HasKey(x => x.QuestionId).HasName("PK_Question");

        builder.Property(x => x.QuestionId)
            .HasColumnName("QuestionID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();
        builder.Property(x => x.DifficultyId)
            .HasColumnName("DifficultyID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(x => x.Grade)
            .HasColumnName("Grade");
        builder.Property(x => x.Status)
            .HasColumnName("Status")
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(x => x.QuestionType)
            .HasColumnName("QuestionType")
            .HasMaxLength(30)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(x => x.IsActive)
            .HasColumnName("IsActive");
    }
}
