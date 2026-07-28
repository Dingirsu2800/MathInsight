using MathInsight.Modules.Grading_Analytics.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.Grading_Analytics.Persistence.Configurations;

public sealed class QuestionVersionConfiguration : IEntityTypeConfiguration<QuestionVersion>
{
    public void Configure(EntityTypeBuilder<QuestionVersion> builder)
    {
        builder.ToTable("QuestionVersion", table => table.ExcludeFromMigrations());
        builder.HasKey(x => x.VersionId);
        builder.Property(x => x.VersionId).HasColumnName("VersionID").HasMaxLength(36).IsUnicode(false);
        builder.Property(x => x.QuestionId).HasColumnName("QuestionID").HasMaxLength(36).IsUnicode(false);
        builder.Property(x => x.QuestionContent).HasColumnName("QuestionContent");
        builder.Property(x => x.QuestionAnswer).HasColumnName("QuestionAnswer");
        builder.Property(x => x.AnswersSnapshot).HasColumnName("AnswersSnapshot");
        builder.Property(x => x.PictureUrl).HasColumnName("PictureUrl").HasMaxLength(255).IsUnicode(false);
        builder.Property(x => x.VersionNumber).HasColumnName("VersionNumber");
        builder.Property(x => x.SnapshotSchemaVersion).HasColumnName("SnapshotSchemaVersion");
    }
}
