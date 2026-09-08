using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Modules.QuestionBank.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Commands.Common;

internal static class QuestionReportBlockerState
{
    public static async Task<bool> HasBlockingReviewAsync(
        QuestionBankDbContext context,
        string questionId,
        CancellationToken cancellationToken)
    {
        if (await context.QuestionReportIncidents.AnyAsync(
                incident => incident.QuestionId == questionId &&
                            (incident.Status == "Open" || incident.Status == "PendingAdminReview"),
                cancellationToken))
        {
            return true;
        }

        return await context.QuestionReports.AnyAsync(
            report => report.QuestionId == questionId &&
                      report.IncidentId == null &&
                      (report.ReporterRole == "Expert" || report.ReporterRole == "Admin") &&
                      (report.Status == QuestionReportWorkflow.Pending ||
                       report.Status == QuestionReportWorkflow.PendingFix ||
                       report.Status == QuestionReportWorkflow.PendingReview),
            cancellationToken);
    }
}
