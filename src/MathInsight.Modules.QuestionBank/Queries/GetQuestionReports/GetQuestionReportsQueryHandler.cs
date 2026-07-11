using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Queries.GetQuestionReports;

public sealed class GetQuestionReportsQueryHandler
    : IRequestHandler<GetQuestionReportsQuery, Result<IReadOnlyList<QuestionReportResponse>>>
{
    private const string ActionRequiredStatus = "ActionRequired";
    private readonly QuestionBankDbContext _context;

    public GetQuestionReportsQueryHandler(QuestionBankDbContext context)
    {
        _context = context;
    }

    public async Task<Result<IReadOnlyList<QuestionReportResponse>>> Handle(
        GetQuestionReportsQuery request,
        CancellationToken cancellationToken)
    {
        var status = NormalizeStatus(request.Status);
        if (status is null)
            return Result<IReadOnlyList<QuestionReportResponse>>.Failure(QuestionBankErrors.ReportStatusInvalid);

        var question = await _context.Questions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.QuestionId == request.QuestionId, cancellationToken);

        if (question is null)
            return Result<IReadOnlyList<QuestionReportResponse>>.Failure(QuestionBankErrors.QuestionNotFound);

        if (!string.Equals(question.ExpertId, request.RequestingExpertId, StringComparison.OrdinalIgnoreCase))
            return Result<IReadOnlyList<QuestionReportResponse>>.Failure(QuestionBankErrors.ReportAccessForbidden);

        var reports = await (
                from report in _context.QuestionReports.AsNoTracking()
                join account in _context.AccountReadModels.AsNoTracking()
                    on report.ReporterAccountId equals account.AccountId into accounts
                from account in accounts.DefaultIfEmpty()
                where report.QuestionId == request.QuestionId &&
                      (status == ActionRequiredStatus
                          ? report.Status == "Pending" ||
                            report.Status == "PendingFix" ||
                            report.Status == "PendingReview"
                          : report.Status == status)
                orderby report.CreatedTime descending, report.ReportId descending
                select new QuestionReportResponse(
                    report.ReportId,
                    report.QuestionId,
                    report.ReporterAccountId,
                    account == null ? null : account.FirstName + " " + account.LastName,
                    report.ReporterRole,
                    report.ReportReason,
                    report.Status,
                    report.CreatedTime,
                    report.ResolvedTime,
                    report.ResolvedBy,
                    report.ReviewNote,
                    report.SubmittedTime,
                    report.ReviewedTime,
                    report.ReviewedBy))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<QuestionReportResponse>>.Success(reports);
    }

    private static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return ActionRequiredStatus;

        return status.Trim().ToUpperInvariant() switch
        {
            "PENDING" => ActionRequiredStatus,
            "ACTIONREQUIRED" => ActionRequiredStatus,
            "PENDINGFIX" => "PendingFix",
            "PENDINGREVIEW" => "PendingReview",
            "RESOLVED" => "Resolved",
            "DISMISSED" => "Dismissed",
            _ => null
        };
    }
}
