using System.Data;
using MathInsight.Modules.QuestionBank.Commands.Common;
using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Modules.QuestionBank.Services;
using MathInsight.Shared.Results;
using MathInsight.Shared.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MathInsight.Modules.QuestionBank.Commands.SubmitQuestionReportIncident;

public sealed class SubmitQuestionReportIncidentCommandHandler
    : IRequestHandler<SubmitQuestionReportIncidentCommand, Result<QuestionReportIncidentResponse>>
{
    private readonly QuestionBankDbContext _context;
    private readonly QuestionMutationService _mutationService;
    private readonly IPublisher? _publisher;

    public SubmitQuestionReportIncidentCommandHandler(QuestionBankDbContext context, IPublisher? publisher = null)
    {
        _context = context;
        _mutationService = new QuestionMutationService(context);
        _publisher = publisher;
    }

    public async Task<Result<QuestionReportIncidentResponse>> Handle(SubmitQuestionReportIncidentCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.ExpertAccountId))
            return Result<QuestionReportIncidentResponse>.Failure(QuestionBankErrors.ReportAccessForbidden);
        if (command.Request.ReportDecisions is null ||
            string.IsNullOrWhiteSpace(command.Request.SubmissionKey) ||
            command.Request.SubmissionKey.Length > 64 ||
            string.IsNullOrWhiteSpace(command.Request.ExpectedQuestionVersionId))
            return Result<QuestionReportIncidentResponse>.Failure(QuestionBankErrors.QuestionRequestInvalid);

        var reference = await _context.QuestionReportIncidents.AsNoTracking()
            .Where(item => item.IncidentId == command.IncidentId)
            .Select(item => new { item.QuestionId })
            .FirstOrDefaultAsync(cancellationToken);
        if (reference is null)
            return Result<QuestionReportIncidentResponse>.Failure(QuestionBankErrors.ReportNotFound);

        return await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using IDbContextTransaction? transaction = QuestionReportSqlServerLock.IsSupported(_context)
                ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            if (transaction is not null)
                await QuestionReportSqlServerLock.LockQuestionAsync(_context, reference.QuestionId, cancellationToken);

            var incident = await _context.QuestionReportIncidents
                .Include(item => item.Question)
                .Include(item => item.Reports)
                .FirstOrDefaultAsync(item => item.IncidentId == command.IncidentId, cancellationToken);
            if (incident is null)
                return Result<QuestionReportIncidentResponse>.Failure(QuestionBankErrors.ReportNotFound);
            if (!string.Equals(incident.Question.ExpertId, command.ExpertAccountId, StringComparison.OrdinalIgnoreCase))
                return Result<QuestionReportIncidentResponse>.Failure(QuestionBankErrors.ReportAccessForbidden);

            var fingerprint = IncidentSubmissionFingerprint.Create(command.Request);
            if (incident.SubmissionKey == command.Request.SubmissionKey)
            {
                if (incident.SubmissionPayloadHash == fingerprint)
                    return Result<QuestionReportIncidentResponse>.Success(ToResponse(incident));
                return Result<QuestionReportIncidentResponse>.Failure(QuestionBankErrors.ReportSubmissionKeyConflict);
            }

            if (incident.Revision != command.Request.ExpectedRevision || incident.Status != "Open")
                return Result<QuestionReportIncidentResponse>.Failure(QuestionBankErrors.ReportIncidentConflict);
            if (!string.Equals(incident.QuestionVersionId, command.Request.ExpectedQuestionVersionId, StringComparison.Ordinal))
                return Result<QuestionReportIncidentResponse>.Failure(QuestionBankErrors.ReportVersionStale);
            if (command.Request.ResolutionAction is not ("NoScoreChange" or "InvalidateAndAwardFull"))
                return Result<QuestionReportIncidentResponse>.Failure(QuestionBankErrors.ReportResolutionActionInvalid);

            var activeReports = incident.Reports
                .Where(item => item.Status == QuestionReportWorkflow.Pending ||
                    item.Status == QuestionReportWorkflow.PendingFix ||
                    item.Status == QuestionReportWorkflow.PendingReview)
                .ToList();
            if (activeReports.Count != command.Request.ReportDecisions.Count ||
                command.Request.ReportDecisions.Select(item => item.ReportId).Distinct(StringComparer.Ordinal).Count() != activeReports.Count ||
                activeReports.Any(item => !command.Request.ReportDecisions.Any(decision => decision.ReportId == item.ReportId)) ||
                command.Request.ReportDecisions.Any(item => item.Disposition is not ("Resolved" or "Dismissed")))
            {
                return Result<QuestionReportIncidentResponse>.Failure(QuestionBankErrors.ReportStatusInvalid);
            }

            var decisionsByReportId = command.Request.ReportDecisions
                .ToDictionary(item => item.ReportId, StringComparer.Ordinal);
            var requiresCorrection = command.Request.ResolutionAction == "InvalidateAndAwardFull" ||
                activeReports.Any(item => decisionsByReportId[item.ReportId].Disposition == "Resolved");
            if (requiresCorrection == (command.Request.Correction is null))
                return Result<QuestionReportIncidentResponse>.Failure(QuestionBankErrors.QuestionRequestInvalid);

            string? submittedCorrectionVersionId = null;
            if (requiresCorrection)
            {
                var mutation = await _mutationService.ApplyAsync(
                    incident.QuestionId, command.Request.Correction!, command.ExpertAccountId, cancellationToken,
                    rejectPendingAdminReview: false);
                if (mutation.IsFailure)
                    return Result<QuestionReportIncidentResponse>.Failure(mutation.Error!);
                submittedCorrectionVersionId = mutation.Value!.Version.VersionId;
            }

            var now = DateTime.UtcNow;
            incident.SubmittedCorrectionVersionId = submittedCorrectionVersionId;
            incident.ProposedResolutionAction = command.Request.ResolutionAction;
            incident.Status = incident.RequiresAdminReview
                ? "PendingAdminReview"
                : command.Request.ResolutionAction == "InvalidateAndAwardFull"
                    ? "AdjustmentPending"
                    : "Closed";
            incident.SubmissionKey = command.Request.SubmissionKey;
            incident.SubmissionPayloadHash = fingerprint;
            incident.Revision++;
            incident.UpdatedTime = now;
            foreach (var activeReport in activeReports)
            {
                var decision = command.Request.ReportDecisions.Single(item => item.ReportId == activeReport.ReportId);
                activeReport.ProposedStatus = decision.Disposition;
                activeReport.ProposedReviewNote = decision.ReviewNote?.Trim();
                activeReport.SubmittedTime = now;
                if (!incident.RequiresAdminReview)
                {
                    activeReport.Status = decision.Disposition;
                    activeReport.ReviewNote = decision.ReviewNote?.Trim();
                    activeReport.ResolutionAction = command.Request.ResolutionAction;
                    activeReport.ResolvedBy = command.ExpertAccountId;
                    activeReport.ResolvedTime = now;
                }
            }
            if (incident.RequiresAdminReview)
            {
                var adminReport = activeReports.SingleOrDefault(item =>
                    item.ReporterRole == "Admin" &&
                    string.Equals(item.ReporterAccountId, incident.AssignedAdminId, StringComparison.OrdinalIgnoreCase));
                if (adminReport is null || adminReport.Status != QuestionReportWorkflow.PendingFix)
                    return Result<QuestionReportIncidentResponse>.Failure(QuestionBankErrors.ReportStatusInvalid);
                adminReport.Status = QuestionReportWorkflow.PendingReview;
            }
            else
            {
                incident.ApprovedResolutionAction = command.Request.ResolutionAction;
                incident.AdjustmentStatus = command.Request.ResolutionAction == "InvalidateAndAwardFull"
                    ? "Pending"
                    : "NotRequired";
                incident.Question.Status = "Approved";
                incident.Question.UpdatedTime = now;
            }
            incident.Question.Status = incident.RequiresAdminReview ? "Reported" : incident.Question.Status;

            await _context.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);

            if (_publisher is not null)
            {
                if (incident.RequiresAdminReview && !string.IsNullOrWhiteSpace(incident.AssignedAdminId))
                {
                    await _publisher.Publish(new NotificationRequestedEvent(
                        incident.AssignedAdminId,
                        "Cần duyệt xử lý báo cáo câu hỏi",
                        "Một bản sửa câu hỏi đã được gửi để bạn duyệt.",
                        $"/admin/question-reports/{incident.IncidentId}",
                        $"question-report:{incident.IncidentId}:submission:{incident.Revision}"), cancellationToken);
                }
                else
                {
                    foreach (var finalizedReport in activeReports)
                    {
                        await _publisher.Publish(new NotificationRequestedEvent(
                            finalizedReport.ReporterAccountId,
                            "Báo cáo câu hỏi đã được xử lý",
                            finalizedReport.Status == "Resolved"
                                ? "Báo cáo của bạn đã được xử lý."
                                : "Báo cáo của bạn đã được xem xét và không được chấp nhận.",
                            $"/questions/{incident.QuestionId}",
                            $"question-report:{finalizedReport.ReportId}:{finalizedReport.Status}"), cancellationToken);
                    }
                }
            }
            return Result<QuestionReportIncidentResponse>.Success(ToResponse(incident));
        });
    }

    private static QuestionReportIncidentResponse ToResponse(Entities.QuestionReportIncident incident) => new(
        incident.IncidentId, incident.QuestionId, incident.QuestionVersionId, incident.Revision, incident.Status,
        incident.RequiresAdminReview, incident.SubmittedCorrectionVersionId, incident.ProposedResolutionAction, incident.AdjustmentStatus);
}
