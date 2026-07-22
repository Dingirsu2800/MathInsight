using MathInsight.Modules.QuestionBank.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.QuestionBank.Persistence.Configurations;

public class QuestionVersionConfiguration : IEntityTypeConfiguration<QuestionVersion>
{
    public void Configure(EntityTypeBuilder<QuestionVersion> builder)
    {
        builder.ToTable(nameof(QuestionVersion));

        builder.HasKey(version => version.VersionId)
            .HasName("PK_QuestionVersion");

        builder.Property(version => version.VersionId)
            .HasColumnName("VersionID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .ValueGeneratedNever();

        builder.Property(version => version.QuestionId)
            .HasColumnName("QuestionID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(version => version.QuestionContent)
            .HasColumnName("QuestionContent")
            .IsRequired();

        builder.Property(version => version.QuestionAnswer)
            .HasColumnName("QuestionAnswer")
            .IsRequired();

        builder.Property(version => version.AnswersSnapshot)
            .HasColumnName("AnswersSnapshot")
            .IsRequired();

        builder.Property(version => version.PictureUrl)
            .HasColumnName("PictureUrl")
            .HasMaxLength(255)
            .IsUnicode(false);

        builder.Property(version => version.VersionNumber)
            .HasColumnName("VersionNumber")
            .IsRequired();

        builder.Property(version => version.SnapshotSchemaVersion)
            .HasColumnName("SnapshotSchemaVersion")
            .HasDefaultValue((short)2)
            .IsRequired();

        builder.Property(version => version.CreatedTime)
            .HasColumnName("CreatedTime")
            .HasColumnType("datetime2(0)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(version => version.ExpertId)
            .HasColumnName("ExpertID")
            .HasMaxLength(36)
            .IsUnicode(false)
            .IsRequired();

        builder.HasIndex(version => version.QuestionId)
            .HasDatabaseName("IX_QuestionVersion_QuestionID");

        builder.HasIndex(version => new { version.QuestionId, version.VersionNumber })
            .IsUnique()
            .HasDatabaseName("UX_QuestionVersion_Question_VersionNumber");

        builder.HasOne(version => version.Question)
            .WithMany(question => question.Versions)
            .HasForeignKey(version => version.QuestionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_QuestionVersion_Question_QuestionID");
    }
}
