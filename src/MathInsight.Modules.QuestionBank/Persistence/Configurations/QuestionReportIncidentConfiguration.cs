using MathInsight.Modules.QuestionBank.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MathInsight.Modules.QuestionBank.Persistence.Configurations;

public sealed class QuestionReportIncidentConfiguration : IEntityTypeConfiguration<QuestionReportIncident>
{
    public void Configure(EntityTypeBuilder<QuestionReportIncident> builder)
    {
        builder.ToTable("QuestionReportIncident");
        builder.HasKey(item => item.IncidentId).HasName("PK_QuestionReportIncident");
        builder.Property(item => item.IncidentId).HasColumnName("IncidentID").HasMaxLength(36).IsUnicode(false).ValueGeneratedNever();
        builder.Property(item => item.QuestionId).HasColumnName("QuestionID").HasMaxLength(36).IsUnicode(false).IsRequired();
        builder.Property(item => item.QuestionVersionId).HasColumnName("QuestionVersionID").HasMaxLength(36).IsUnicode(false).IsRequired();
        builder.Property(item => item.Revision).HasColumnName("Revision").IsConcurrencyToken().HasDefaultValue(0);
        builder.Property(item => item.Status).HasColumnName("Status").HasMaxLength(30).IsUnicode(false).HasDefaultValue("Open");
        builder.Property(item => item.RequiresAdminReview).HasColumnName("RequiresAdminReview").HasDefaultValue(false);
        builder.Property(item => item.AssignedAdminId).HasColumnName("AssignedAdminID").HasMaxLength(36).IsUnicode(false);
        builder.Property(item => item.SubmittedCorrectionVersionId).HasColumnName("SubmittedCorrectionVersionID").HasMaxLength(36).IsUnicode(false);
        builder.Property(item => item.ProposedResolutionAction).HasColumnName("ProposedResolutionAction").HasMaxLength(30).IsUnicode(false);
        builder.Property(item => item.ApprovedResolutionAction).HasColumnName("ApprovedResolutionAction").HasMaxLength(30).IsUnicode(false);
        builder.Property(item => item.AdjustmentStatus).HasColumnName("AdjustmentStatus").HasMaxLength(30).IsUnicode(false);
        builder.Property(item => item.SubmissionKey).HasColumnName("SubmissionKey").HasMaxLength(64).IsUnicode(false);
        builder.Property(item => item.SubmissionPayloadHash).HasColumnName("SubmissionPayloadHash").HasMaxLength(64).IsUnicode(false);
        builder.Property(item => item.CreatedTime).HasColumnName("CreatedTime").HasColumnType("datetime2(0)").IsRequired();
        builder.Property(item => item.UpdatedTime).HasColumnName("UpdatedTime").HasColumnType("datetime2(0)");

        builder.HasIndex(item => new { item.QuestionId, item.QuestionVersionId })
            .IsUnique()
            .HasDatabaseName("UQ_QuestionReportIncident_Question_Version");
        builder.HasOne(item => item.Question).WithMany().HasForeignKey(item => item.QuestionId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestionReportIncident_Question_QuestionID");
        builder.HasOne(item => item.QuestionVersion).WithMany().HasForeignKey(item => item.QuestionVersionId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_QuestionReportIncident_QuestionVersion_QuestionVersionID");
    }
}
