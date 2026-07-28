using MathInsight.Modules.TestGen.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.TestGen.Persistence.Configurations;

public sealed class QuestionVersionReadModelConfiguration : IEntityTypeConfiguration<QuestionVersionReadModel>
{
    public void Configure(EntityTypeBuilder<QuestionVersionReadModel> builder)
    {
        builder.ToTable("QuestionVersion", table => table.ExcludeFromMigrations());
        builder.HasKey(x => x.VersionId).HasName("PK_QuestionVersion");
        builder.Property(x => x.VersionId).HasColumnName("VersionID").HasMaxLength(36).IsUnicode(false);
        builder.Property(x => x.QuestionId).HasColumnName("QuestionID").HasMaxLength(36).IsUnicode(false);
        builder.Property(x => x.VersionNumber).HasColumnName("VersionNumber");
        builder.Property(x => x.SnapshotSchemaVersion).HasColumnName("SnapshotSchemaVersion");
        builder.Property(x => x.AnswersSnapshot).HasColumnName("AnswersSnapshot");
        builder.Property(x => x.CreatedTime).HasColumnName("CreatedTime").HasColumnType("datetime2(0)");
        builder.HasIndex(x => new { x.QuestionId, x.VersionNumber })
            .IsUnique()
            .HasDatabaseName("UQ_QuestionVersion_Question_VersionNumber");
    }
}
