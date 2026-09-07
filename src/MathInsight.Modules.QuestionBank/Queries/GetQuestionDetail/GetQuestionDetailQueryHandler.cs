using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Queries.GetQuestionDetail;

public sealed class GetQuestionDetailQueryHandler
    : IRequestHandler<GetQuestionDetailQuery, Result<QuestionDetailResponse>>
{
    private readonly QuestionBankDbContext _context;

    public GetQuestionDetailQueryHandler(QuestionBankDbContext context)
    {
        _context = context;
    }

    public async Task<Result<QuestionDetailResponse>> Handle(
        GetQuestionDetailQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.QuestionId))
            return Result<QuestionDetailResponse>.Failure(QuestionBankErrors.QuestionIdRequired);

        var question = await _context.Questions
            .AsNoTracking()
            .Where(question => question.QuestionId == request.QuestionId)
            .Select(question => new QuestionDetailResponse(
                question.QuestionId,
                question.QuestionContent,
                question.SolutionContent,
                question.PictureUrl,
                question.DifficultyId,
                question.Difficulty.DifficultyName,
                question.Difficulty.LevelValue,
                question.Grade,
                question.Status,
                question.QuestionType,
                question.ExpertId,
                _context.AccountReadModels
                    .Where(account => account.AccountId == question.ExpertId)
                    .Select(account => account.FirstName + " " + account.LastName)
                    .FirstOrDefault(),
                question.DefaultWeight,
                question.IsActive,
                question.CreatedTime,
                question.UpdatedTime,
                question.QuestionTopics
                    .OrderByDescending(topic => topic.IsPrimary)
                    .ThenBy(topic => topic.Tag.DisplayOrder)
                    .Select(topic => new QuestionTopicResponse(
                        topic.TagId,
                        topic.Tag.TagName,
                        topic.IsPrimary))
                    .ToList(),
                question.Answers
                    .Where(answer => !answer.IsArchived)
                    .OrderBy(answer => answer.AnswerId)
                    .Select(answer => new QuestionAnswerResponse(
                        answer.AnswerId,
                        answer.AnswerContent,
                        answer.IsCorrect))
                    .ToList(),
                question.Parts
                    .Where(part => !part.IsArchived)
                    .OrderBy(part => part.PartOrder)
                    .Select(part => new QuestionPartResponse(
                        part.PartId,
                        part.PartOrder,
                        part.PartLabel,
                        part.PartContent,
                        part.PartType,
                        part.CorrectBoolean,
                        part.CorrectText,
                        part.CorrectNumeric,
                        part.NumericTolerance,
                        part.Explanation,
                        part.DefaultWeight))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        if (question is null)
            return Result<QuestionDetailResponse>.Failure(QuestionBankErrors.QuestionNotFound);

        var eligibility = await GetReportEligibilityAsync(question, request, cancellationToken);
        return Result<QuestionDetailResponse>.Success(question with { ReportEligibility = eligibility });
    }

    private async Task<ReportEligibilityResponse> GetReportEligibilityAsync(
        QuestionDetailResponse question,
        GetQuestionDetailQuery request,
        CancellationToken cancellationToken)
    {
        var accountId = request.RequestingAccountId?.Trim();
        var role = request.RequestingRole?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(accountId) || role is not ("EXPERT" or "ADMIN" or "STUDENT"))
            return new(false, "AUTH_REQUIRED", null, null, null, null, null);

        var versionId = await _context.QuestionVersions
            .AsNoTracking()
            .Where(version => version.QuestionId == question.QuestionId)
            .OrderByDescending(version => version.VersionNumber)
            .Select(version => version.VersionId)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(versionId))
            return new(false, "QUESTION_VERSION_UNAVAILABLE", null, null, null, null, null);

        var existingReport = await _context.QuestionReports
            .AsNoTracking()
            .Where(report => report.QuestionId == question.QuestionId &&
                             report.ReporterAccountId == accountId &&
                             report.QuestionVersionId == versionId)
            .OrderByDescending(report => report.CreatedTime)
            .Select(report => new { report.ReportId, report.Status, report.IncidentId })
            .FirstOrDefaultAsync(cancellationToken);
        var incident = existingReport?.IncidentId is { Length: > 0 } incidentId
            ? await _context.QuestionReportIncidents
                .AsNoTracking()
                .Where(item => item.IncidentId == incidentId)
                .Select(item => new
                {
                    item.Status,
                    item.RequiresAdminReview,
                    ResolutionAction = item.ApprovedResolutionAction ?? item.ProposedResolutionAction,
                    item.AdjustmentStatus
                })
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        if (existingReport is not null)
        {
            return new(false, "ALREADY_REPORTED_VERSION", existingReport.ReportId, existingReport.Status,
                existingReport.IncidentId, incident?.Status, versionId,
                incident?.RequiresAdminReview ?? false,
                incident?.ResolutionAction,
                incident?.AdjustmentStatus);
        }

        if (role == "EXPERT" && string.Equals(question.ExpertId, accountId, StringComparison.OrdinalIgnoreCase))
            return new(false, "SELF_REPORT_FORBIDDEN", null, null, null, null, versionId);

        if (role is "EXPERT" or "ADMIN" && (!question.IsActive || question.Status is not ("Approved" or "Reported")))
            return new(false, "QUESTION_NOT_REPORTABLE", null, null, null, null, versionId);

        return new(true, null, null, null, null, null, versionId);
    }
}
