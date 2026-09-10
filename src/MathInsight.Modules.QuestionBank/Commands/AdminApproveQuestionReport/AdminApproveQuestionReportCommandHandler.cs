using MathInsight.Modules.QuestionBank.Commands.Common;
using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
using MathInsight.Shared.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace MathInsight.Modules.QuestionBank.Commands.AdminApproveQuestionReport;

public sealed class AdminApproveQuestionReportCommandHandler
    : IRequestHandler<AdminApproveQuestionReportCommand, Result<QuestionReportResponse>>
{
    private readonly QuestionBankDbContext _context;
    private readonly IPublisher? _publisher;

    public AdminApproveQuestionReportCommandHandler(QuestionBankDbContext context, IPublisher? publisher = null)
    {
        _context = context;
        _publisher = publisher;
    }

    public async Task<Result<QuestionReportResponse>> Handle(
        AdminApproveQuestionReportCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.AdminAccountId))
            return Result<QuestionReportResponse>.Failure(QuestionBankErrors.ReportAccessForbidden);

        var reportReference = await _context.QuestionReports
            .AsNoTracking()
            .Where(report => report.ReportId == command.ReportId)
            .Select(report => new { report.QuestionId })
            .FirstOrDefaultAsync(cancellationToken);

        if (reportReference is null)
            return Result<QuestionReportResponse>.Failure(QuestionBankErrors.ReportNotFound);

        return await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
        await using IDbContextTransaction? transaction = QuestionReportSqlServerLock.IsSupported(_context)
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        if (transaction is not null)
            await QuestionReportSqlServerLock.LockQuestionAsync(_context, reportReference.QuestionId, cancellationToken);

        var report = await _context.QuestionReports
            .Include(item => item.Question)
            .Include(item => item.Incident)
            .FirstOrDefaultAsync(item => item.ReportId == command.ReportId, cancellationToken);

        if (report is null)
            return Result<QuestionReportResponse>.Failure(QuestionBankErrors.ReportNotFound);

        if (report.ReporterRole != "Admin" ||
            !string.Equals(report.ReporterAccountId, command.AdminAccountId, StringComparison.OrdinalIgnoreCase))
        {
            return Result<QuestionReportResponse>.Failure(QuestionBankErrors.ReportAccessForbidden);
        }

        if (report.Status != QuestionReportWorkflow.PendingReview)
            return Result<QuestionReportResponse>.Failure(QuestionBankErrors.ReportAlreadyHandled);

        List<QuestionReport> activeIncidentReports = [];
        if (report.Incident is not null)
        {
            await _context.Entry(report.Incident)
                .Collection(item => item.Reports)
                .LoadAsync(cancellationToken);

            activeIncidentReports = report.Incident.Reports
                .Where(IsActiveReport)
                .ToList();
            var requiresCorrection = activeIncidentReports.Any(item => item.ProposedStatus == QuestionReportWorkflow.Resolved);
            var submittedVersionMatchesLatest = !requiresCorrection || await _context.QuestionVersions
                .Where(item => item.QuestionId == report.QuestionId)
                .OrderByDescending(item => item.VersionNumber)
                .Select(item => item.VersionId == report.Incident.SubmittedCorrectionVersionId)
                .FirstOrDefaultAsync(cancellationToken);
            if (!submittedVersionMatchesLatest || string.IsNullOrWhiteSpace(report.Incident.ProposedResolutionAction) ||
                activeIncidentReports.Any(item => string.IsNullOrWhiteSpace(item.ProposedStatus)))
            {
                return Result<QuestionReportResponse>.Failure(QuestionBankErrors.ReportAlreadyHandled);
            }
        }

        var now = DateTime.UtcNow;
        report.Status = QuestionReportWorkflow.Resolved;
        report.ReviewedTime = now;
        report.ReviewedBy = command.AdminAccountId;
        report.ResolvedTime = now;
        report.ResolvedBy = command.AdminAccountId;

        // Reports on the same snapshot version are one incident. Admin approval authorizes
        // expert resolution of that incident; it does not silently resolve student reports.
        if (report.Incident is not null)
        {
            report.Incident.RequiresAdminReview = false;
            report.Incident.ApprovedResolutionAction = report.Incident.ProposedResolutionAction;
            report.Incident.Status = report.Incident.ProposedResolutionAction == "InvalidateAndAwardFull"
                ? "AdjustmentPending"
                : "Closed";
            report.Incident.AdjustmentStatus = report.Incident.ProposedResolutionAction == "InvalidateAndAwardFull"
                ? "Pending"
                : "NotRequired";
            report.Incident.AssignedAdminId = command.AdminAccountId;
            report.Incident.Revision++;
            report.Incident.UpdatedTime = now;

            foreach (var relatedReport in activeIncidentReports)
            {
                relatedReport.Status = relatedReport.ProposedStatus!;
                relatedReport.ReviewNote = relatedReport.ProposedReviewNote;
                relatedReport.ResolutionAction = report.Incident.ApprovedResolutionAction;
                relatedReport.ResolvedTime = now;
                relatedReport.ResolvedBy = command.AdminAccountId;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (!await QuestionReportBlockerState.HasBlockingReviewAsync(_context, report.QuestionId, cancellationToken) &&
            report.Question.Status == "Reported")
        {
            report.Question.Status = "Approved";
            report.Question.UpdatedTime = now;
            await _context.SaveChangesAsync(cancellationToken);
        }

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        if (_publisher is not null && report.Incident is not null)
        {
            await _publisher.Publish(new NotificationRequestedEvent(
                report.Question.ExpertId,
                "Bản sửa câu hỏi đã được duyệt",
                "Admin đã duyệt bản sửa và quyết định xử lý báo cáo của bạn.",
                $"/expert/questions/{report.QuestionId}/reports",
                $"question-report:{report.Incident.IncidentId}:review:{report.Incident.Revision}:approved"), cancellationToken);

            foreach (var relatedReport in report.Incident.Reports)
            {
                await _publisher.Publish(new NotificationRequestedEvent(
                    relatedReport.ReporterAccountId,
                    "Báo cáo câu hỏi đã được xử lý",
                    relatedReport.Status == "Resolved"
                        ? "Báo cáo của bạn đã được xử lý."
                        : "Báo cáo của bạn đã được xem xét và không được chấp nhận.",
                    $"/questions/{report.QuestionId}",
                    $"question-report:{relatedReport.ReportId}:{relatedReport.Status}"), cancellationToken);
            }
        }

        return Result<QuestionReportResponse>.Success(
            await QuestionReportResponseMapper.CreateAsync(_context, report, cancellationToken));
        });
    }

    private static bool IsActiveReport(QuestionReport report) =>
        report.Status is QuestionReportWorkflow.Pending or
            QuestionReportWorkflow.PendingFix or
            QuestionReportWorkflow.PendingReview;
}
