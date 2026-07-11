using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Modules.QuestionBank.Commands.Common;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace MathInsight.Modules.QuestionBank.Commands.HandleQuestionReport;

public sealed class HandleQuestionReportCommandHandler
    : IRequestHandler<HandleQuestionReportCommand, Result<QuestionReportResponse>>
{
    private readonly QuestionBankDbContext _context;

    public HandleQuestionReportCommandHandler(QuestionBankDbContext context)
    {
        _context = context;
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

        if (report.Status != "Pending")
            return Result<QuestionReportResponse>.Failure(QuestionBankErrors.ReportAlreadyHandled);

        report.Status = targetStatus;
        report.ResolvedTime = DateTime.UtcNow;
        report.ResolvedBy = command.ExpertAccountId;

        var otherBlockingReportsRemain = await _context.QuestionReports.AnyAsync(
            item => item.QuestionId == report.QuestionId &&
                    item.ReportId != report.ReportId &&
                    item.Status == "Pending" &&
                    (item.ReporterRole == "Expert" || item.ReporterRole == "Admin"),
            cancellationToken);

        if (!otherBlockingReportsRemain && report.Question.Status == "Reported")
            report.Question.Status = "Approved";

        await _context.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

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
            report.ResolvedBy));
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
}
