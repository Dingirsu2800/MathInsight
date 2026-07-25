using MathInsight.Modules.Testing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Testing.Persistence.Configurations;

public sealed class QuestionVersionConfiguration : IEntityTypeConfiguration<QuestionVersion>
{
    public void Configure(EntityTypeBuilder<QuestionVersion> builder)
    {
        builder.ToTable("QuestionVersion", table => table.ExcludeFromMigrations());
        builder.HasKey(item => item.VersionId).HasName("PK_QuestionVersion");
        builder.Property(item => item.VersionId).HasColumnName("VersionID").HasMaxLength(36).IsUnicode(false);
        builder.Property(item => item.QuestionId).HasColumnName("QuestionID").HasMaxLength(36).IsUnicode(false);
        builder.Property(item => item.QuestionContent).HasColumnName("QuestionContent");
        builder.Property(item => item.QuestionAnswer).HasColumnName("QuestionAnswer");
        builder.Property(item => item.AnswersSnapshot).HasColumnName("AnswersSnapshot");
        builder.Property(item => item.PictureUrl).HasColumnName("PictureUrl").HasMaxLength(255).IsUnicode(false);
        builder.Property(item => item.SnapshotSchemaVersion).HasColumnName("SnapshotSchemaVersion");
    }
}
