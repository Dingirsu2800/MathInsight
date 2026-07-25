using MathInsight.Modules.QuestionBank.Commands.Common;
using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
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

    public AdminRejectQuestionReportCommandHandler(QuestionBankDbContext context)
    {
        _context = context;
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

        report.Status = QuestionReportWorkflow.PendingFix;
        report.ReviewNote = reviewNote;
        var now = DateTime.UtcNow;
        report.ReviewedTime = now;
        report.ReviewedBy = command.AdminAccountId;
        report.Question.Status = "Rejected";
        report.Question.UpdatedTime = now;

        await _context.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return Result<QuestionReportResponse>.Success(
            await QuestionReportResponseMapper.CreateAsync(_context, report, cancellationToken));
    }
}
