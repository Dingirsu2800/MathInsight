using MathInsight.Modules.QuestionBank.Commands.Common;
using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
using MathInsight.Shared.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace MathInsight.Modules.QuestionBank.Commands.AdminRejectQuestionReport;

public sealed class AdminRejectQuestionReportCommandHandler
    : IRequestHandler<AdminRejectQuestionReportCommand, Result<QuestionReportResponse>>
{
    private const int MaxReviewNoteLength = 2000;
    private readonly QuestionBankDbContext _context;
    private readonly IPublisher? _publisher;

    public AdminRejectQuestionReportCommandHandler(QuestionBankDbContext context, IPublisher? publisher = null)
    {
        _context = context;
        _publisher = publisher;
    }

    public async Task<Result<QuestionReportResponse>> Handle(
        AdminRejectQuestionReportCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.AdminAccountId))
            return Result<QuestionReportResponse>.Failure(QuestionBankErrors.ReportAccessForbidden);

        var reviewNote = command.Request.ReviewNote?.Trim();
        if (string.IsNullOrWhiteSpace(reviewNote))
            return Result<QuestionReportResponse>.Failure(QuestionBankErrors.ReviewNoteRequired);

        if (reviewNote.Length > MaxReviewNoteLength)
            return Result<QuestionReportResponse>.Failure(QuestionBankErrors.ReviewNoteTooLong);

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

        report.Status = QuestionReportWorkflow.PendingFix;
        report.ReviewNote = reviewNote;
        var now = DateTime.UtcNow;
        report.ReviewedTime = now;
        report.ReviewedBy = command.AdminAccountId;

        if (report.Incident is not null)
        {
            await _context.Entry(report.Incident)
                .Collection(item => item.Reports)
                .LoadAsync(cancellationToken);

            report.Incident.Status = "Open";
            report.Incident.SubmittedCorrectionVersionId = null;
            report.Incident.ProposedResolutionAction = null;
            report.Incident.ApprovedResolutionAction = null;
            report.Incident.AdjustmentStatus = null;
            report.Incident.SubmissionKey = null;
            report.Incident.SubmissionPayloadHash = null;
            report.Incident.Revision++;
            report.Incident.UpdatedTime = now;

            foreach (var relatedReport in report.Incident.Reports)
            {
                relatedReport.ProposedStatus = null;
                relatedReport.ProposedReviewNote = null;
                relatedReport.SubmittedTime = null;
                if (relatedReport.Status == QuestionReportWorkflow.PendingReview)
                    relatedReport.Status = relatedReport.ReporterRole == "Admin"
                        ? QuestionReportWorkflow.PendingFix
                        : QuestionReportWorkflow.Pending;
            }
        }
        report.Question.Status = "Rejected";
        report.Question.UpdatedTime = now;

        await _context.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        if (_publisher is not null && report.Incident is not null)
        {
            await _publisher.Publish(new NotificationRequestedEvent(
                report.Question.ExpertId,
                "Bản sửa câu hỏi cần được cập nhật",
                "Admin đã từ chối bản sửa và gửi phản hồi để bạn cập nhật.",
                $"/expert/questions/{report.QuestionId}/reports",
                $"question-report:{report.Incident.IncidentId}:review:{report.Incident.Revision}:rejected"), cancellationToken);
        }

        return Result<QuestionReportResponse>.Success(
            await QuestionReportResponseMapper.CreateAsync(_context, report, cancellationToken));
        });
    }
}
