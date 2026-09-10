using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Queries.GetQuestionReportIncident;

public sealed class GetQuestionReportIncidentQueryHandler
    : IRequestHandler<GetQuestionReportIncidentQuery, Result<QuestionReportIncidentDetailResponse>>
{
    private readonly QuestionBankDbContext _context;

    public GetQuestionReportIncidentQueryHandler(QuestionBankDbContext context) => _context = context;

    public async Task<Result<QuestionReportIncidentDetailResponse>> Handle(
        GetQuestionReportIncidentQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IncidentId) || string.IsNullOrWhiteSpace(request.RequestingAccountId))
            return Result<QuestionReportIncidentDetailResponse>.Failure(QuestionBankErrors.ReportNotFound);

        var incident = await _context.QuestionReportIncidents
            .AsNoTracking()
            .Include(item => item.Question)
            .FirstOrDefaultAsync(item => item.IncidentId == request.IncidentId, cancellationToken);
        if (incident is null)
            return Result<QuestionReportIncidentDetailResponse>.Failure(QuestionBankErrors.ReportNotFound);

        var canRead = request.RequestingRole.Trim().ToUpperInvariant() switch
        {
            "EXPERT" => string.Equals(incident.Question.ExpertId, request.RequestingAccountId, StringComparison.OrdinalIgnoreCase),
            "ADMIN" => string.Equals(incident.AssignedAdminId, request.RequestingAccountId, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
        if (!canRead)
            return Result<QuestionReportIncidentDetailResponse>.Failure(QuestionBankErrors.ReportAccessForbidden);

        var versions = await _context.QuestionVersions
            .AsNoTracking()
            .Where(item => item.VersionId == incident.QuestionVersionId || item.VersionId == incident.SubmittedCorrectionVersionId)
            .ToListAsync(cancellationToken);
        var original = versions.SingleOrDefault(item => item.VersionId == incident.QuestionVersionId);
        if (original is null)
            return Result<QuestionReportIncidentDetailResponse>.Failure(QuestionBankErrors.ReportNotFound);

        var reports = await (
            from report in _context.QuestionReports.AsNoTracking()
            join account in _context.AccountReadModels.AsNoTracking()
                on report.ReporterAccountId equals account.AccountId into accounts
            from account in accounts.DefaultIfEmpty()
            where report.IncidentId == incident.IncidentId
            orderby report.CreatedTime, report.ReportId
            select new QuestionReportIncidentReportResponse(
                report.ReportId,
                report.ReporterAccountId,
                account == null ? null : account.FirstName + " " + account.LastName,
                report.ReporterRole,
                report.ReportReason,
                report.Status,
                report.ProposedStatus,
                report.ProposedReviewNote,
                report.ReviewNote,
                report.QuestionVersionId,
                report.ResolutionAction,
                report.SessionId,
                report.CreatedTime,
                report.SubmittedTime,
                report.ReviewedTime,
                report.ResolvedTime,
                report.ScoreAdjustedTime))
            .ToListAsync(cancellationToken);

        return Result<QuestionReportIncidentDetailResponse>.Success(new QuestionReportIncidentDetailResponse(
            incident.IncidentId,
            incident.QuestionId,
            incident.Revision,
            incident.Status,
            incident.RequiresAdminReview,
            incident.AssignedAdminId,
            incident.SubmittedCorrectionVersionId,
            incident.ProposedResolutionAction,
            incident.ApprovedResolutionAction,
            incident.AdjustmentStatus,
            ToVersionResponse(original, incident.Question.Status),
            versions.SingleOrDefault(item => item.VersionId == incident.SubmittedCorrectionVersionId) is { } submitted
                ? ToVersionResponse(submitted, incident.Question.Status)
                : null,
            reports));
    }

    private static QuestionVersionResponse ToVersionResponse(Entities.QuestionVersion version, string questionStatus) => new(
        version.VersionId,
        version.QuestionId,
        version.QuestionContent,
        version.QuestionAnswer,
        version.AnswersSnapshot,
        version.PictureUrl,
        version.VersionNumber,
        version.SnapshotSchemaVersion,
        version.CreatedTime,
        version.ExpertId,
        null,
        questionStatus);
}
