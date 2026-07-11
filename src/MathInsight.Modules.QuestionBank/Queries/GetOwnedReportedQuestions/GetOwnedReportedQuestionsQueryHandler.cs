using MathInsight.Modules.QuestionBank.Contracts.Common;
using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Queries.GetOwnedReportedQuestions;

public sealed class GetOwnedReportedQuestionsQueryHandler
    : IRequestHandler<GetOwnedReportedQuestionsQuery, Result<PagedResponse<ReportedQuestionListItemResponse>>>
{
    private const int DefaultPageIndex = 1;
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 100;
    private const string ActionRequiredStatus = "ActionRequired";

    private readonly QuestionBankDbContext _context;

    public GetOwnedReportedQuestionsQueryHandler(QuestionBankDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedResponse<ReportedQuestionListItemResponse>>> Handle(
        GetOwnedReportedQuestionsQuery request,
        CancellationToken cancellationToken)
    {
        var status = NormalizeStatus(request.Status);
        if (status is null)
            return Result<PagedResponse<ReportedQuestionListItemResponse>>.Failure(QuestionBankErrors.ReportStatusInvalid);

        var pageIndex = request.PageIndex <= 0 ? DefaultPageIndex : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? DefaultPageSize : Math.Min(request.PageSize, MaxPageSize);

        var reportsForOwner = _context.QuestionReports
            .AsNoTracking()
            .Where(report => report.Question.ExpertId == request.OwnerExpertId);

        var filteredReports = status == ActionRequiredStatus
            ? reportsForOwner.Where(report => report.Status == "Pending" ||
                                             report.Status == "PendingFix" ||
                                             report.Status == "PendingReview")
            : reportsForOwner.Where(report => report.Status == status);

        var groupedReports = filteredReports
            .GroupBy(report => report.QuestionId)
            .Select(group => new
            {
                QuestionId = group.Key,
                LatestReportAt = group.Max(report => report.CreatedTime)
            });

        var totalCount = await groupedReports.CountAsync(cancellationToken);
        var page = await groupedReports
            .OrderByDescending(item => item.LatestReportAt)
            .ThenBy(item => item.QuestionId)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var questionIds = page.Select(item => item.QuestionId).ToList();
        var filteredPageReports = await filteredReports
            .Where(report => questionIds.Contains(report.QuestionId))
            .OrderByDescending(report => report.CreatedTime)
            .ThenByDescending(report => report.ReportId)
            .ToListAsync(cancellationToken);

        var pendingCounts = await _context.QuestionReports
            .AsNoTracking()
            .Where(report => questionIds.Contains(report.QuestionId) &&
                             (report.Status == "Pending" ||
                              report.Status == "PendingFix" ||
                              report.Status == "PendingReview"))
            .GroupBy(report => report.QuestionId)
            .Select(group => new { QuestionId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.QuestionId, item => item.Count, cancellationToken);

        var questions = await _context.Questions
            .AsNoTracking()
            .Include(question => question.QuestionTopics)
                .ThenInclude(topic => topic.Tag)
            .Where(question => questionIds.Contains(question.QuestionId))
            .ToDictionaryAsync(question => question.QuestionId, cancellationToken);

        var items = page
            .Where(item => questions.ContainsKey(item.QuestionId))
            .Select(item =>
            {
                var question = questions[item.QuestionId];
                var reports = filteredPageReports
                    .Where(report => report.QuestionId == question.QuestionId)
                    .OrderByDescending(report => report.CreatedTime)
                    .ToList();
                var latestReport = reports[0];

                return new ReportedQuestionListItemResponse(
                    question.QuestionId,
                    question.QuestionContent,
                    question.Grade,
                    question.Status,
                    question.QuestionType,
                    question.QuestionTopics
                        .OrderByDescending(topic => topic.IsPrimary)
                        .ThenBy(topic => topic.Tag.DisplayOrder)
                        .Select(topic => new QuestionTopicSummaryResponse(
                            topic.TagId,
                            topic.Tag.TagName,
                            topic.IsPrimary))
                        .ToList(),
                    pendingCounts.GetValueOrDefault(question.QuestionId),
                    latestReport.ReportReason,
                    latestReport.CreatedTime,
                    reports
                        .Select(report => report.ReporterRole)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList());
            })
            .ToList();

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        return Result<PagedResponse<ReportedQuestionListItemResponse>>.Success(
            new PagedResponse<ReportedQuestionListItemResponse>(
                items,
                pageIndex,
                pageSize,
                totalCount,
                totalPages));
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
