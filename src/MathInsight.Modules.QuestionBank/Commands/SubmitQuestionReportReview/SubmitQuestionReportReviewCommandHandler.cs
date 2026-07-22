using MathInsight.Modules.QuestionBank.Commands.Common;
using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace MathInsight.Modules.QuestionBank.Commands.SubmitQuestionReportReview;

public sealed class SubmitQuestionReportReviewCommandHandler
    : IRequestHandler<SubmitQuestionReportReviewCommand, Result<QuestionReportResponse>>
{
    private readonly QuestionBankDbContext _context;

    public SubmitQuestionReportReviewCommandHandler(QuestionBankDbContext context)
    {
        _context = context;
    }

    public async Task<Result<QuestionReportResponse>> Handle(
        SubmitQuestionReportReviewCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.ExpertAccountId))
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

        if (!string.Equals(report.Question.ExpertId, command.ExpertAccountId, StringComparison.OrdinalIgnoreCase))
            return Result<QuestionReportResponse>.Failure(QuestionBankErrors.ReportAccessForbidden);

        if (report.ReporterRole != "Admin")
            return Result<QuestionReportResponse>.Failure(QuestionBankErrors.AdminReportRequiresReview);

        if (report.Status != QuestionReportWorkflow.PendingFix)
            return Result<QuestionReportResponse>.Failure(QuestionBankErrors.ReportAlreadyHandled);

        var now = DateTime.UtcNow;
        report.Status = QuestionReportWorkflow.PendingReview;
        report.SubmittedTime = now;
        report.Question.Status = "Reported";
        report.Question.UpdatedTime = now;

        await _context.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return Result<QuestionReportResponse>.Success(
            await QuestionReportResponseMapper.CreateAsync(_context, report, cancellationToken));
    }
}
