using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Learning_Lecture.Entities;

namespace MathInsight.Modules.Learning_Lecture.Persistence.Configurations;

public class DiscussionReportConfiguration : IEntityTypeConfiguration<DiscussionReport>
{
    public void Configure(EntityTypeBuilder<DiscussionReport> builder)
    {
        builder.ToTable(nameof(DiscussionReport));

        builder.HasKey(x => x.ReportId);

        builder.Property(x => x.ReportId).HasMaxLength(36);
        builder.Property(x => x.DiscussionQuestionId).HasMaxLength(36);
        builder.Property(x => x.DiscussionAnswerId).HasMaxLength(36);
        builder.Property(x => x.ReporterAccountId).HasMaxLength(36).IsRequired();
        builder.Property(x => x.ReportReason).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Pending");
        builder.Property(x => x.CreatedTime);
        builder.Property(x => x.ResolvedTime);
        builder.Property(x => x.ResolverAccountId).HasColumnName("ResolvedByAccountID").HasMaxLength(36);

        builder.HasOne(x => x.Question)
            .WithMany(x => x.Reports)
            .HasForeignKey(x => x.DiscussionQuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Answer)
            .WithMany(x => x.Reports)
            .HasForeignKey(x => x.DiscussionAnswerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint("CK_DiscussionReport_Target",
            "([DiscussionQuestionId] IS NOT NULL AND [DiscussionAnswerId] IS NULL) OR ([DiscussionQuestionId] IS NULL AND [DiscussionAnswerId] IS NOT NULL)"));
    }
}
