using MathInsight.Modules.QuestionBank.Commands.Common;
using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace MathInsight.Modules.QuestionBank.Commands.AdminApproveQuestionReport;

public sealed class AdminApproveQuestionReportCommandHandler
    : IRequestHandler<AdminApproveQuestionReportCommand, Result<QuestionReportResponse>>
{
    private readonly QuestionBankDbContext _context;

    public AdminApproveQuestionReportCommandHandler(QuestionBankDbContext context)
    {
        _context = context;
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

        if (report.ReporterRole != "Admin" ||
            !string.Equals(report.ReporterAccountId, command.AdminAccountId, StringComparison.OrdinalIgnoreCase))
        {
            return Result<QuestionReportResponse>.Failure(QuestionBankErrors.ReportAccessForbidden);
        }

        if (report.Status != QuestionReportWorkflow.PendingReview)
            return Result<QuestionReportResponse>.Failure(QuestionBankErrors.ReportAlreadyHandled);

        var now = DateTime.UtcNow;
        report.Status = QuestionReportWorkflow.Resolved;
        report.ReviewedTime = now;
        report.ReviewedBy = command.AdminAccountId;
        report.ResolvedTime = now;
        report.ResolvedBy = command.AdminAccountId;

        var hasPendingExpertReport = await _context.QuestionReports.AnyAsync(
            item => item.QuestionId == report.QuestionId &&
                    item.ReportId != report.ReportId &&
                    item.ReporterRole == "Expert" &&
                    item.Status == QuestionReportWorkflow.Pending,
            cancellationToken);

        if (!hasPendingExpertReport)
        {
            report.Question.Status = "Approved";
            report.Question.UpdatedTime = now;
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return Result<QuestionReportResponse>.Success(
            await QuestionReportResponseMapper.CreateAsync(_context, report, cancellationToken));
    }
}
