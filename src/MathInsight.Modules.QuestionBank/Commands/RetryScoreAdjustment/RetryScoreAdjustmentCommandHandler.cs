using MathInsight.Modules.QuestionBank.Commands.Common;
using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
using MathInsight.Shared.Scoring;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Commands.RetryScoreAdjustment;

public sealed class RetryScoreAdjustmentCommandHandler
    : IRequestHandler<RetryScoreAdjustmentCommand, Result<QuestionReportResponse>>
{
    private readonly QuestionBankDbContext _context;
    private readonly IScoreAdjustmentService _scoreAdjustmentService;

    public RetryScoreAdjustmentCommandHandler(
        QuestionBankDbContext context,
        IScoreAdjustmentService scoreAdjustmentService)
    {
        _context = context;
        _scoreAdjustmentService = scoreAdjustmentService;
    }

    public async Task<Result<QuestionReportResponse>> Handle(
        RetryScoreAdjustmentCommand command,
        CancellationToken cancellationToken)
    {
        var report = await _context.QuestionReports
            .Include(item => item.Question)
            .FirstOrDefaultAsync(item => item.ReportId == command.ReportId, cancellationToken);

        if (report is null)
            return Result<QuestionReportResponse>.Failure(QuestionBankErrors.ReportNotFound);
        if (!string.Equals(report.Question.ExpertId, command.ExpertAccountId, StringComparison.OrdinalIgnoreCase))
            return Result<QuestionReportResponse>.Failure(QuestionBankErrors.ReportAccessForbidden);
        if (report.Status != "Resolved" ||
            report.ResolutionAction != "InvalidateAndAwardFull" ||
            report.ScoreAdjustedTime is not null)
        {
            return Result<QuestionReportResponse>.Failure(QuestionBankErrors.ScoreAdjustmentNotRetryable);
        }

        await _scoreAdjustmentService.AdjustInvalidQuestionVersionAsync(report.ReportId, cancellationToken);
        await _context.Entry(report).ReloadAsync(cancellationToken);

        return Result<QuestionReportResponse>.Success(
            await QuestionReportResponseMapper.CreateAsync(_context, report, cancellationToken));
    }
}
