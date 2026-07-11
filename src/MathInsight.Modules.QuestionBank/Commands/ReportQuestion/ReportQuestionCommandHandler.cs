using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Modules.QuestionBank.Commands.Common;
using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
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

    public ReportQuestionCommandHandler(QuestionBankDbContext context)
    {
        _context = context;
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

        await using IDbContextTransaction? transaction = QuestionReportSqlServerLock.IsSupported(_context)
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        if (transaction is not null)
            await QuestionReportSqlServerLock.LockQuestionAsync(_context, command.QuestionId, cancellationToken);

        var question = await _context.Questions
            .FirstOrDefaultAsync(question => question.QuestionId == command.QuestionId, cancellationToken);

        if (question is null)
            return Result<ReportQuestionResponse>.Failure(QuestionBankErrors.QuestionNotFound);

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

        var hasActiveReportFromReporter = await _context.QuestionReports.AnyAsync(
            report => report.QuestionId == question.QuestionId &&
                      report.ReporterAccountId == command.ReporterAccountId &&
                      (report.Status == QuestionReportWorkflow.Pending ||
                       report.Status == QuestionReportWorkflow.PendingFix ||
                       report.Status == QuestionReportWorkflow.PendingReview),
            cancellationToken);

        if (hasActiveReportFromReporter)
            return Result<ReportQuestionResponse>.Failure(QuestionBankErrors.ReportAlreadyPending);

        var createdTime = DateTime.UtcNow;
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
            CreatedTime = createdTime
        };

        _context.QuestionReports.Add(report);

        if (reporterRole is "Expert" or "Admin" && question.Status == "Approved")
            question.Status = "Reported";

        await _context.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return Result<ReportQuestionResponse>.Success(new ReportQuestionResponse(
            report.ReportId,
            question.QuestionId,
            report.ReporterRole,
            report.ReportReason,
            report.Status,
            report.CreatedTime,
            question.Status,
            question.IsActive));
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
