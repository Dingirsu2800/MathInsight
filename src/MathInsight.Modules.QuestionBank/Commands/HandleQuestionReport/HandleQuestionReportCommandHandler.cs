using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Modules.QuestionBank.Commands.Common;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
using MathInsight.Shared.Scoring;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace MathInsight.Modules.QuestionBank.Commands.HandleQuestionReport;

public sealed class HandleQuestionReportCommandHandler
    : IRequestHandler<HandleQuestionReportCommand, Result<QuestionReportResponse>>
{
    private readonly QuestionBankDbContext _context;
    private readonly IScoreAdjustmentService? _scoreAdjustmentService;

    public HandleQuestionReportCommandHandler(
        QuestionBankDbContext context,
        IScoreAdjustmentService? scoreAdjustmentService = null)
    {
        _context = context;
        _scoreAdjustmentService = scoreAdjustmentService;
    }

    public async Task<Result<QuestionReportResponse>> Handle(
        HandleQuestionReportCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.ExpertAccountId))
            return Result<QuestionReportResponse>.Failure(QuestionBankErrors.ReportAccessForbidden);

        var targetStatus = NormalizeHandledStatus(command.Request.Status);
        if (targetStatus is null)
            return Result<QuestionReportResponse>.Failure(QuestionBankErrors.ReportStatusInvalid);

        var resolutionAction = NormalizeResolutionAction(command.Request.ResolutionAction, targetStatus);
        if (resolutionAction is null)
            return Result<QuestionReportResponse>.Failure(QuestionBankErrors.ReportResolutionActionInvalid);

        var reportReference = await _context.QuestionReports
            .AsNoTracking()
            .Where(item => item.ReportId == command.ReportId)
            .Select(item => new { item.QuestionId })
            .FirstOrDefaultAsync(cancellationToken);

        if (reportReference is null)
            return Result<QuestionReportResponse>.Failure(QuestionBankErrors.ReportNotFound);

        await using IDbContextTransaction? transaction = QuestionReportSqlServerLock.IsSupported(_context)
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        if (transaction is not null)
            await QuestionReportSqlServerLock.LockQuestionAsync(_context, reportReference.QuestionId, cancellationToken);

        var report = await _context.QuestionReports
            .Include(item => item.Question)
            .FirstOrDefaultAsync(item => item.ReportId == command.ReportId, cancellationToken);

        if (report is null)
            return Result<QuestionReportResponse>.Failure(QuestionBankErrors.ReportNotFound);

        if (!string.Equals(report.Question.ExpertId, command.ExpertAccountId, StringComparison.OrdinalIgnoreCase))
            return Result<QuestionReportResponse>.Failure(QuestionBankErrors.ReportAccessForbidden);

        if (report.ReporterRole == "Admin")
            return Result<QuestionReportResponse>.Failure(QuestionBankErrors.AdminReportRequiresReview);

        if (report.Status != QuestionReportWorkflow.Pending)
            return Result<QuestionReportResponse>.Failure(QuestionBankErrors.ReportAlreadyHandled);

        if (resolutionAction == "InvalidateAndAwardFull")
        {
            if (report.ReporterRole != "Student" ||
                string.IsNullOrWhiteSpace(report.SessionId) ||
                string.IsNullOrWhiteSpace(report.QuestionVersionId))
            {
                return Result<QuestionReportResponse>.Failure(QuestionBankErrors.ReportSessionContextInvalid);
            }

            var reportedVersionNumber = await _context.QuestionVersions
                .Where(version => version.VersionId == report.QuestionVersionId &&
                                  version.QuestionId == report.QuestionId)
                .Select(version => (int?)version.VersionNumber)
                .FirstOrDefaultAsync(cancellationToken);
            var latestVersionNumber = await _context.QuestionVersions
                .Where(version => version.QuestionId == report.QuestionId)
                .Select(version => (int?)version.VersionNumber)
                .MaxAsync(cancellationToken);

            if (reportedVersionNumber is null ||
                (report.Question.IsActive && latestVersionNumber <= reportedVersionNumber))
            {
                return Result<QuestionReportResponse>.Failure(
                    QuestionBankErrors.QuestionFixRequiredBeforeScoreAdjustment);
            }
        }

        report.Status = targetStatus;
        report.ResolutionAction = resolutionAction;
        report.ResolvedTime = DateTime.UtcNow;
        report.ResolvedBy = command.ExpertAccountId;

        var otherBlockingReportsRemain = await _context.QuestionReports.AnyAsync(
            item => item.QuestionId == report.QuestionId &&
                    item.ReportId != report.ReportId &&
                    ((item.ReporterRole == "Expert" && item.Status == QuestionReportWorkflow.Pending) ||
                     (item.ReporterRole == "Admin" &&
                      (item.Status == QuestionReportWorkflow.PendingFix ||
                       item.Status == QuestionReportWorkflow.PendingReview))),
            cancellationToken);

        if (!otherBlockingReportsRemain && report.Question.Status == "Reported")
        {
            report.Question.Status = "Approved";
            report.Question.UpdatedTime = report.ResolvedTime.Value;
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        if (resolutionAction == "InvalidateAndAwardFull")
        {
            if (_scoreAdjustmentService is null)
                throw new InvalidOperationException("Score adjustment service is not registered.");

            await _scoreAdjustmentService.AdjustInvalidQuestionVersionAsync(
                report.ReportId,
                cancellationToken);
            await _context.Entry(report).ReloadAsync(cancellationToken);
        }

        var reporterName = await _context.AccountReadModels
            .Where(account => account.AccountId == report.ReporterAccountId)
            .Select(account => account.FirstName + " " + account.LastName)
            .FirstOrDefaultAsync(cancellationToken);

        return Result<QuestionReportResponse>.Success(new QuestionReportResponse(
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
            report.ReviewedBy,
            report.SessionId,
            report.QuestionVersionId,
            report.ResolutionAction,
            report.ScoreAdjustedTime));
    }

    private static string? NormalizeHandledStatus(string? status)
    {
        return status?.Trim().ToUpperInvariant() switch
        {
            "RESOLVED" => "Resolved",
            "DISMISSED" => "Dismissed",
            _ => null
        };
    }

    private static string? NormalizeResolutionAction(string? action, string targetStatus)
    {
        if (targetStatus == "Dismissed")
            return string.IsNullOrWhiteSpace(action) ||
                   string.Equals(action, "NoScoreChange", StringComparison.OrdinalIgnoreCase)
                ? "NoScoreChange"
                : null;

        if (string.IsNullOrWhiteSpace(action))
            return "NoScoreChange";

        return action.Trim().ToUpperInvariant() switch
        {
            "NOSCORECHANGE" => "NoScoreChange",
            "INVALIDATEANDAWARDFULL" => "InvalidateAndAwardFull",
            _ => null
        };
    }
}
