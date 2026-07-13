using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MathInsight.Modules.Learning_Lecture.Entities;

namespace MathInsight.Modules.Learning_Lecture.Persistence.Configurations;

public class DiscussionReportConfiguration : IEntityTypeConfiguration<DiscussionReport>
{
    public void Configure(EntityTypeBuilder<DiscussionReport> builder)
    {
        builder.ToTable("discussion_reports");

        builder.HasKey(x => x.ReportId);

        builder.Property(x => x.ReportId).HasColumnName("report_id").HasMaxLength(36);
        builder.Property(x => x.DiscussionQuestionId).HasColumnName("discussion_question_id").HasMaxLength(36);
        builder.Property(x => x.DiscussionAnswerId).HasColumnName("discussion_answer_id").HasMaxLength(36);
        builder.Property(x => x.ReporterAccountId).HasColumnName("reporter_account_id").HasMaxLength(36).IsRequired();
        builder.Property(x => x.ReportReason).HasColumnName("report_reason").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired().HasDefaultValue("Pending");
        builder.Property(x => x.CreatedTime).HasColumnName("created_time");
        builder.Property(x => x.ResolvedTime).HasColumnName("resolved_time");
        builder.Property(x => x.ResolverAccountId).HasColumnName("resolver_account_id").HasMaxLength(36);

        builder.ToTable(t => t.HasCheckConstraint("CK_DiscussionReport_Target",
            "([discussion_question_id] IS NOT NULL AND [discussion_answer_id] IS NULL) OR ([discussion_question_id] IS NULL AND [discussion_answer_id] IS NOT NULL)"));
    }
}
