using MathInsight.Modules.QuestionBank.Commands.Common;
using MathInsight.Modules.QuestionBank.Contracts.Common;
using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Queries.GetAdminQuestionReports;

public sealed class GetAdminQuestionReportsQueryHandler
    : IRequestHandler<GetAdminQuestionReportsQuery, Result<PagedResponse<AdminQuestionReportListItemResponse>>>
{
    private const int DefaultPageIndex = 1;
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 100;

    private readonly QuestionBankDbContext _context;

    public GetAdminQuestionReportsQueryHandler(QuestionBankDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedResponse<AdminQuestionReportListItemResponse>>> Handle(
        GetAdminQuestionReportsQuery request,
        CancellationToken cancellationToken)
    {
        var status = NormalizeStatus(request.Status);
        if (status is null)
            return Result<PagedResponse<AdminQuestionReportListItemResponse>>.Failure(QuestionBankErrors.ReportStatusInvalid);

        var pageIndex = request.PageIndex <= 0 ? DefaultPageIndex : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? DefaultPageSize : Math.Min(request.PageSize, MaxPageSize);

        var reports =
            from report in _context.QuestionReports.AsNoTracking()
            join question in _context.Questions.AsNoTracking()
                on report.QuestionId equals question.QuestionId
            join expert in _context.AccountReadModels.AsNoTracking()
                on question.ExpertId equals expert.AccountId into experts
            from expert in experts.DefaultIfEmpty()
            where report.ReporterRole == "Admin" &&
                  report.ReporterAccountId == request.AdminAccountId &&
                  report.Status == status
            select new
            {
                Report = report,
                Question = question,
                Expert = expert
            };

        var totalCount = await reports.CountAsync(cancellationToken);
        var orderedReports = await reports
            .OrderByDescending(report => report.Report.SubmittedTime ?? report.Report.CreatedTime)
            .ThenByDescending(report => report.Report.ReportId)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = orderedReports
            .Select(item => new AdminQuestionReportListItemResponse(
                item.Report.ReportId,
                item.Question.QuestionId,
                item.Question.QuestionContent,
                item.Question.Status,
                item.Question.ExpertId,
                item.Expert == null ? null : item.Expert.FirstName + " " + item.Expert.LastName,
                item.Report.ReportReason,
                item.Report.ReviewNote,
                item.Report.Status,
                item.Report.CreatedTime,
                item.Report.SubmittedTime,
                item.Report.ReviewedTime,
                item.Report.ReviewedBy))
            .ToList();

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        return Result<PagedResponse<AdminQuestionReportListItemResponse>>.Success(
            new PagedResponse<AdminQuestionReportListItemResponse>(
                items,
                pageIndex,
                pageSize,
                totalCount,
                totalPages));
    }

    private static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return QuestionReportWorkflow.PendingReview;

        return status.Trim().ToUpperInvariant() switch
        {
            "PENDINGFIX" => QuestionReportWorkflow.PendingFix,
            "PENDINGREVIEW" => QuestionReportWorkflow.PendingReview,
            "RESOLVED" => QuestionReportWorkflow.Resolved,
            _ => null
        };
    }
}
