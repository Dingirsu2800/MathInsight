using MathInsight.Modules.TestGen.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.TestGen.Persistence.Configurations;

public sealed class AnswerReadModelConfiguration : IEntityTypeConfiguration<AnswerReadModel>
{
    public void Configure(EntityTypeBuilder<AnswerReadModel> builder)
    {
        builder.ToTable("Answer", table => table.ExcludeFromMigrations());
        builder.HasKey(x => x.AnswerId).HasName("PK_Answer");
        builder.Property(x => x.AnswerId).HasColumnName("AnswerID").HasMaxLength(36).IsUnicode(false);
        builder.Property(x => x.QuestionId).HasColumnName("QuestionID").HasMaxLength(36).IsUnicode(false);
        builder.Property(x => x.IsCorrect).HasColumnName("IsCorrect");
        builder.Property(x => x.IsArchived).HasColumnName("IsArchived");
        builder.HasIndex(x => x.QuestionId)
            .HasFilter("[IsArchived] = 0")
            .HasDatabaseName("IX_Answer_Current_Question");
    }
}
