using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Modules.QuestionBank.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Commands.Common;

internal static class QuestionReportResponseMapper
{
    public static async Task<QuestionReportResponse> CreateAsync(
        QuestionBankDbContext context,
        QuestionReport report,
        CancellationToken cancellationToken)
    {
        var reporterName = await context.AccountReadModels
            .Where(account => account.AccountId == report.ReporterAccountId)
            .Select(account => account.FirstName + " " + account.LastName)
            .FirstOrDefaultAsync(cancellationToken);

        return new QuestionReportResponse(
            report.ReportId,
            report.QuestionId,
            report.ReporterAccountId,
            reporterName,
            report.ReporterRole,
            report.ReportReason,
            report.Status,
            report.CreatedTime,
            report.ResolvedTime,
            report.ResolvedBy,
            report.ReviewNote,
            report.SubmittedTime,
            report.ReviewedTime,
            report.ReviewedBy);
    }
}
