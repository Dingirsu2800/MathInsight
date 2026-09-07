using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Modules.QuestionBank.Commands.Common;
using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
using MathInsight.Shared.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace MathInsight.Modules.QuestionBank.Commands.ReportQuestion;

public sealed class ReportQuestionCommandHandler
    : IRequestHandler<ReportQuestionCommand, Result<ReportQuestionResponse>>
{
    private const int MaxReasonLength = 2000;
    private readonly QuestionBankDbContext _context;
    private readonly IPublisher? _publisher;

    public ReportQuestionCommandHandler(QuestionBankDbContext context, IPublisher? publisher = null)
    {
        _context = context;
        _publisher = publisher;
    }

    public async Task<Result<ReportQuestionResponse>> Handle(
        ReportQuestionCommand command,
        CancellationToken cancellationToken)
    {
        var reason = command.Request.ReportReason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            return Result<ReportQuestionResponse>.Failure(QuestionBankErrors.ReportReasonRequired);

        if (reason.Length > MaxReasonLength)
            return Result<ReportQuestionResponse>.Failure(QuestionBankErrors.ReportReasonTooLong);

        var reporterRole = NormalizeReporterRole(command.ReporterRole);
        if (reporterRole is null || string.IsNullOrWhiteSpace(command.ReporterAccountId))
            return Result<ReportQuestionResponse>.Failure(QuestionBankErrors.ReportAccessForbidden);

        if (command.SessionId is not null && command.QuestionVersionId is null)
            return Result<ReportQuestionResponse>.Failure(QuestionBankErrors.ReportSessionContextInvalid);

        return await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
        await using IDbContextTransaction? transaction = QuestionReportSqlServerLock.IsSupported(_context)
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        if (transaction is not null)
            await QuestionReportSqlServerLock.LockQuestionAsync(_context, command.QuestionId, cancellationToken);

        var question = await _context.Questions
            .FirstOrDefaultAsync(question => question.QuestionId == command.QuestionId, cancellationToken);

        if (question is null)
            return Result<ReportQuestionResponse>.Failure(QuestionBankErrors.QuestionNotFound);

        var questionVersionId = command.QuestionVersionId;
        if (questionVersionId is not null)
        {
            var versionExists = await _context.QuestionVersions.AnyAsync(
                version => version.VersionId == questionVersionId &&
                           version.QuestionId == question.QuestionId,
                cancellationToken);
            if (!versionExists)
                return Result<ReportQuestionResponse>.Failure(QuestionBankErrors.ReportSessionContextInvalid);
        }
        else
        {
            questionVersionId = await _context.QuestionVersions
                .Where(version => version.QuestionId == question.QuestionId)
                .OrderByDescending(version => version.VersionNumber)
                .Select(version => version.VersionId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (reporterRole == "Admin")
        {
            var hasActiveAdminWorkflow = await _context.QuestionReports.AnyAsync(
                report => report.QuestionId == question.QuestionId &&
                          report.ReporterRole == "Admin" &&
                          (report.Status == QuestionReportWorkflow.PendingFix ||
                           report.Status == QuestionReportWorkflow.PendingReview),
                cancellationToken);

            if (hasActiveAdminWorkflow)
                return Result<ReportQuestionResponse>.Failure(QuestionBankErrors.AdminReportWorkflowAlreadyExists);
        }

        if (reporterRole is "Expert" or "Admin" &&
            (question.Status is not ("Approved" or "Reported") || !question.IsActive))
            return Result<ReportQuestionResponse>.Failure(QuestionBankErrors.QuestionNotReportable);

        if (reporterRole == "Expert" &&
            string.Equals(question.ExpertId, command.ReporterAccountId, StringComparison.OrdinalIgnoreCase))
        {
            return Result<ReportQuestionResponse>.Failure(QuestionBankErrors.QuestionSelfReportForbidden);
        }

        var hasReportFromReporter = await _context.QuestionReports.AnyAsync(
            report => report.QuestionId == question.QuestionId &&
                      report.ReporterAccountId == command.ReporterAccountId &&
                      report.QuestionVersionId == questionVersionId,
            cancellationToken);

        if (hasReportFromReporter)
            return Result<ReportQuestionResponse>.Failure(QuestionBankErrors.ReportAlreadyPending);

        var createdTime = DateTime.UtcNow;
        QuestionReportIncident? incident = null;
        if (!string.IsNullOrWhiteSpace(questionVersionId))
        {
            incident = await _context.QuestionReportIncidents
                .FirstOrDefaultAsync(
                    item => item.QuestionId == question.QuestionId && item.QuestionVersionId == questionVersionId,
                    cancellationToken);
        }

        if (incident is null && !string.IsNullOrWhiteSpace(questionVersionId))
        {
            incident = new QuestionReportIncident
            {
                IncidentId = Guid.NewGuid().ToString(),
                QuestionId = question.QuestionId,
                QuestionVersionId = questionVersionId,
                Status = "Open",
                RequiresAdminReview = reporterRole == "Admin",
                AssignedAdminId = reporterRole == "Admin" ? command.ReporterAccountId : null,
                CreatedTime = createdTime,
                UpdatedTime = createdTime
            };
            _context.QuestionReportIncidents.Add(incident);
        }
        else if (incident is not null)
        {
            if (incident.Status is "Closed" or "AdjustmentPending")
                return Result<ReportQuestionResponse>.Failure(QuestionBankErrors.ReportIncidentClosed);

            if (incident.Status == "PendingAdminReview")
            {
                // The submitted correction cannot be approved after the incident changes.
                incident.Status = "Open";
                incident.SubmittedCorrectionVersionId = null;
                incident.ProposedResolutionAction = null;
                incident.ApprovedResolutionAction = null;
                incident.AdjustmentStatus = null;
                incident.SubmissionKey = null;
                incident.SubmissionPayloadHash = null;

                foreach (var existingReport in await _context.QuestionReports
                    .Where(item => item.IncidentId == incident.IncidentId)
                    .ToListAsync(cancellationToken))
                {
                    existingReport.ProposedStatus = null;
                    existingReport.ProposedReviewNote = null;
                    if (existingReport.Status == QuestionReportWorkflow.PendingReview)
                        existingReport.Status = existingReport.ReporterRole == "Admin"
                            ? QuestionReportWorkflow.PendingFix
                            : QuestionReportWorkflow.Pending;
                }
            }

            if (reporterRole == "Admin" && !incident.RequiresAdminReview)
            {
                incident.RequiresAdminReview = true;
                incident.AssignedAdminId = command.ReporterAccountId;
            }

            incident.Revision++;
            incident.UpdatedTime = createdTime;
        }

        var report = new QuestionReport
        {
            ReportId = Guid.NewGuid().ToString(),
            QuestionId = question.QuestionId,
            ReporterAccountId = command.ReporterAccountId,
            ReporterRole = reporterRole,
            ReportReason = reason,
            Status = reporterRole == "Admin"
                ? QuestionReportWorkflow.PendingFix
                : QuestionReportWorkflow.Pending,
            CreatedTime = createdTime,
            SessionId = command.SessionId,
            QuestionVersionId = questionVersionId,
            IncidentId = incident?.IncidentId
        };

        _context.QuestionReports.Add(report);

        if (reporterRole is "Expert" or "Admin" && question.Status == "Approved")
        {
            question.Status = "Reported";
            question.UpdatedTime = createdTime;
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        if (_publisher is not null)
        {
            await _publisher.Publish(new NotificationRequestedEvent(
                question.ExpertId,
                "Câu hỏi có báo cáo mới",
                "Một câu hỏi của bạn vừa nhận được báo cáo và cần được xem xét.",
                $"/expert/questions/{question.QuestionId}/reports",
                $"question-report:{incident?.IncidentId}:expert"), cancellationToken);
        }

        return Result<ReportQuestionResponse>.Success(new ReportQuestionResponse(
            report.ReportId,
            question.QuestionId,
            report.ReporterRole,
            report.ReportReason,
            report.Status,
            report.CreatedTime,
            question.Status,
            question.IsActive,
            report.SessionId,
            report.QuestionVersionId));
        });
    }

    private static string? NormalizeReporterRole(string? role)
    {
        return role?.Trim().ToUpperInvariant() switch
        {
            "STUDENT" => "Student",
            "EXPERT" => "Expert",
            "ADMIN" => "Admin",
            _ => null
        };
    }
}
