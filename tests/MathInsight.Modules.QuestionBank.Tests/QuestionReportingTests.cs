using MathInsight.Modules.QuestionBank.Commands.HandleQuestionReport;
using MathInsight.Modules.QuestionBank.Commands.Common;
using MathInsight.Modules.QuestionBank.Commands.ReportQuestion;
using MathInsight.Modules.QuestionBank.Commands.SubmitQuestionReportReview;
using MathInsight.Modules.QuestionBank.Commands.UpdateQuestion;
using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Queries.GetQuestionReports;
using MathInsight.Shared.Scoring;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Tests;

public sealed class QuestionReportingTests
{
    [Fact]
    public async Task StudentReport_WithoutSessionAndVersion_IsRejectedWithoutCreatingReport()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "student-context-required", "Approved", true);
        AddVersion(database, question, 1);
        await database.Context.SaveChangesAsync();

        var result = await new ReportQuestionCommandHandler(database.Context).Handle(
            new ReportQuestionCommand(
                question.QuestionId,
                new ReportQuestionRequest { ReportReason = "The answer needs review." },
                "student-1",
                "Student"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ReportSessionContextInvalid, result.Error);
        Assert.Empty(await database.Context.QuestionReports.ToListAsync());
        Assert.Empty(await database.Context.QuestionReportIncidents.ToListAsync());
    }

    [Fact]
    public async Task LegacyHandleRoute_WithIncidentReport_IsRejectedWithoutMutatingReport()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "incident-route-required", "Reported", true);
        var version = AddVersion(database, question, 1);
        var incident = new QuestionReportIncident
        {
            IncidentId = "incident-route-required-v1",
            QuestionId = question.QuestionId,
            QuestionVersionId = version.VersionId,
            Status = "Open",
            CreatedTime = DateTime.UtcNow,
            UpdatedTime = DateTime.UtcNow
        };
        database.Context.QuestionReportIncidents.Add(incident);
        var report = await AddReportAsync(database, question.QuestionId, "student-1", "Student", "Pending");
        report.QuestionVersionId = version.VersionId;
        report.IncidentId = incident.IncidentId;
        await database.Context.SaveChangesAsync();

        var result = await new HandleQuestionReportCommandHandler(database.Context).Handle(
            new HandleQuestionReportCommand(
                report.ReportId,
                new HandleQuestionReportRequest { Status = "Resolved" },
                question.ExpertId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ReportIncidentSubmissionRequired, result.Error);
        Assert.Equal("Pending", report.Status);
        Assert.Equal("Open", incident.Status);
        Assert.Single(await database.Context.QuestionVersions.ToListAsync());
    }

    [Fact]
    public async Task OrdinaryEdit_WithOpenStudentIncident_IsBlockedWithoutCreatingVersion()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "edit-blocked-by-student", "Reported", true);
        await AddDirectChildTopicAsync(database, "topic-1", question.Grade);
        var version = AddVersion(database, question, 1);
        database.Context.QuestionReportIncidents.Add(new QuestionReportIncident
        {
            IncidentId = "edit-blocked-by-student-v1",
            QuestionId = question.QuestionId,
            QuestionVersionId = version.VersionId,
            Status = "Open",
            RequiresAdminReview = false,
            CreatedTime = DateTime.UtcNow,
            UpdatedTime = DateTime.UtcNow
        });
        await database.Context.SaveChangesAsync();

        var result = await new UpdateQuestionCommandHandler(database.Context).Handle(
            new UpdateQuestionCommand(question.QuestionId, CreateUpdateRequest(question), question.ExpertId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ReportIncidentRequiresResolution, result.Error);
        Assert.Single(await database.Context.QuestionVersions.ToListAsync());
        Assert.Equal("Question content", question.QuestionContent);
    }

    [Fact]
    public async Task OrdinaryEdit_WithOpenIncident_ReturnsConflictBeforePayloadValidation()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "edit-invalid-payload-blocked", "Reported", true);
        var version = AddVersion(database, question, 1);
        database.Context.QuestionReportIncidents.Add(new QuestionReportIncident
        {
            IncidentId = "edit-invalid-payload-blocked-v1",
            QuestionId = question.QuestionId,
            QuestionVersionId = version.VersionId,
            Status = "Open",
            CreatedTime = DateTime.UtcNow,
            UpdatedTime = DateTime.UtcNow
        });
        await database.Context.SaveChangesAsync();

        var result = await new UpdateQuestionCommandHandler(database.Context).Handle(
            new UpdateQuestionCommand(question.QuestionId, new UpdateQuestionRequest(), question.ExpertId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ReportIncidentRequiresResolution, result.Error);
        Assert.Single(await database.Context.QuestionVersions.ToListAsync());
    }

    [Fact]
    public async Task LegacySubmitReviewRoute_WithIncidentReport_IsRejectedWithoutMutatingReport()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "incident-submit-required", "Reported", true);
        var version = AddVersion(database, question, 1);
        var incident = new QuestionReportIncident
        {
            IncidentId = "incident-submit-required-v1",
            QuestionId = question.QuestionId,
            QuestionVersionId = version.VersionId,
            Status = "Open",
            RequiresAdminReview = true,
            AssignedAdminId = "admin-1",
            CreatedTime = DateTime.UtcNow,
            UpdatedTime = DateTime.UtcNow
        };
        database.Context.QuestionReportIncidents.Add(incident);
        var report = await AddReportAsync(database, question.QuestionId, "admin-1", "Admin", "PendingFix");
        report.QuestionVersionId = version.VersionId;
        report.IncidentId = incident.IncidentId;
        await database.Context.SaveChangesAsync();

        var result = await new SubmitQuestionReportReviewCommandHandler(database.Context).Handle(
            new SubmitQuestionReportReviewCommand(report.ReportId, question.ExpertId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ReportIncidentSubmissionRequired, result.Error);
        Assert.Equal("PendingFix", report.Status);
        Assert.Equal("Open", incident.Status);
    }

    [Fact]
    public async Task StudentReport_CreatesReportWithoutChangingQuestionStatus()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "student-report", "Approved", true);

        var result = await CreateReportAsync(database, question, "student-1", "Student");

        Assert.True(result.IsSuccess);
        Assert.Equal("Pending", result.Value!.Status);
        Assert.Equal("Approved", question.Status);
        Assert.True(question.IsActive);
        Assert.Single(await database.Context.QuestionReports.ToListAsync());
    }

    [Fact]
    public async Task StudentReport_ForSnapshotVersion_CreatesAndJoinsOneIncident()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "versioned-report", "Approved", true);
        var version = AddVersion(database, question, 1);
        await database.Context.SaveChangesAsync();
        var handler = new ReportQuestionCommandHandler(database.Context);

        var first = await handler.Handle(
            new ReportQuestionCommand(
                question.QuestionId,
                new ReportQuestionRequest { ReportReason = "First reason" },
                "student-1",
                "Student",
                "session-1",
                version.VersionId),
            CancellationToken.None);
        var second = await handler.Handle(
            new ReportQuestionCommand(
                question.QuestionId,
                new ReportQuestionRequest { ReportReason = "Second reason" },
                "student-2",
                "Student",
                "session-2",
                version.VersionId),
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        var incident = Assert.Single(await database.Context.QuestionReportIncidents.ToListAsync());
        var reports = await database.Context.QuestionReports.OrderBy(item => item.ReporterAccountId).ToListAsync();
        Assert.All(reports, report => Assert.Equal(incident.IncidentId, report.IncidentId));
        Assert.Equal(version.VersionId, incident.QuestionVersionId);
    }

    [Theory]
    [InlineData("Rejected", true)]
    [InlineData("Deactivated", false)]
    public async Task StudentReport_HistoricalOrInactiveQuestion_StillCreatesReport(string status, bool isActive)
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, $"student-history-{status}", status, isActive);

        var result = await CreateReportAsync(database, question, "student-1", "Student");

        Assert.True(result.IsSuccess);
        Assert.Equal(status, question.Status);
        Assert.Equal(isActive, question.IsActive);
    }

    [Fact]
    public async Task ExpertReport_ChangesApprovedQuestionToReportedWithoutDeactivatingIt()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "expert-report", "Approved", true);

        var result = await CreateReportAsync(database, question, "expert-2", "Expert");

        Assert.True(result.IsSuccess);
        Assert.Equal("Reported", question.Status);
        Assert.True(question.IsActive);
    }

    [Fact]
    public async Task AdminReport_ChangesApprovedQuestionToReportedWithoutDeactivatingIt()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "admin-report", "Approved", true);

        var result = await CreateReportAsync(database, question, "admin-1", "Admin");

        Assert.True(result.IsSuccess);
        Assert.Equal("Admin", result.Value!.ReporterRole);
        Assert.Equal("PendingFix", result.Value.Status);
        Assert.Equal("Reported", question.Status);
        Assert.True(question.IsActive);
    }

    [Fact]
    public async Task ExpertReport_OwnQuestion_ReturnsForbidden()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "self-report", "Approved", true);

        var result = await CreateReportAsync(database, question, question.ExpertId, "Expert");

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.QuestionSelfReportForbidden, result.Error);
    }

    [Fact]
    public async Task TeacherReport_ReturnsAccessForbidden()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "teacher-report", "Approved", true);

        var result = await CreateReportAsync(database, question, "teacher-1", "Teacher");

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ReportAccessForbidden, result.Error);
    }

    [Fact]
    public async Task DuplicatePendingReport_FromSameAccount_ReturnsConflict()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "duplicate-report", "Approved", true);

        var firstResult = await CreateReportAsync(database, question, "student-1", "Student");
        var duplicateResult = await CreateReportAsync(database, question, "student-1", "Student");

        Assert.True(firstResult.IsSuccess);
        Assert.True(duplicateResult.IsFailure);
        Assert.Equal(QuestionBankErrors.ReportAlreadyPending, duplicateResult.Error);
    }

    [Fact]
    public async Task ResolvedReport_FromSameAccountAndVersion_CannotBeReportedAgain()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "resolved-duplicate-report", "Approved", true);
        var version = AddVersion(database, question, 1);
        await database.Context.SaveChangesAsync();
        var handler = new ReportQuestionCommandHandler(database.Context);

        var first = await handler.Handle(
            new ReportQuestionCommand(
                question.QuestionId,
                new ReportQuestionRequest { ReportReason = "First report." },
                "student-1",
                "Student",
                "session-1",
                version.VersionId),
            CancellationToken.None);
        Assert.True(first.IsSuccess);

        var report = await database.Context.QuestionReports.SingleAsync();
        report.Status = QuestionReportWorkflow.Resolved;
        report.ResolvedTime = DateTime.UtcNow;
        await database.Context.SaveChangesAsync();

        var duplicate = await handler.Handle(
            new ReportQuestionCommand(
                question.QuestionId,
                new ReportQuestionRequest { ReportReason = "Second report." },
                "student-1",
                "Student",
                "session-2",
                version.VersionId),
            CancellationToken.None);

        Assert.True(duplicate.IsFailure);
        Assert.Equal(QuestionBankErrors.ReportAlreadyPending, duplicate.Error);
        Assert.Single(await database.Context.QuestionReports.ToListAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ReportQuestion_WhenReasonIsBlank_ReturnsValidationError(string reason)
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "blank-reason", "Approved", true);

        var result = await CreateReportAsync(database, question, "student-1", "Student", reason);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ReportReasonRequired, result.Error);
    }

    [Fact]
    public async Task ReportQuestion_WhenReasonExceedsLimit_ReturnsValidationError()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "long-reason", "Approved", true);

        var result = await CreateReportAsync(database, question, "student-1", "Student", new string('x', 2001));

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ReportReasonTooLong, result.Error);
    }

    [Theory]
    [InlineData("Rejected", true)]
    [InlineData("Deactivated", false)]
    public async Task ExpertReport_NonReportableQuestion_ReturnsConflict(string status, bool isActive)
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, $"non-reportable-{status}", status, isActive);

        var result = await CreateReportAsync(database, question, "expert-2", "Expert");

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.QuestionNotReportable, result.Error);
    }

    [Fact]
    public async Task ExpertReport_QuestionUsedInTest_StillMovesToReported()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "test-history-report", "Approved", true);
        database.Context.TestQuestionReadModels.Add(new TestQuestionReadModel
        {
            TestId = "test-1",
            QuestionId = question.QuestionId
        });
        await database.Context.SaveChangesAsync();

        var result = await CreateReportAsync(database, question, "expert-2", "Expert");

        Assert.True(result.IsSuccess);
        Assert.Equal("Reported", question.Status);
        Assert.True(question.IsActive);
    }

    [Fact]
    public async Task HandleReport_WhenLastBlockingReportIsHandled_RestoresQuestionToApproved()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "restore-question", "Reported", true);
        var report = await AddReportAsync(database, question.QuestionId, "expert-2", "Expert", "Pending");
        await AddReportAsync(database, question.QuestionId, "student-1", "Student", "Pending");

        var result = await new HandleQuestionReportCommandHandler(database.Context)
            .Handle(
                new HandleQuestionReportCommand(
                    report.ReportId,
                    new HandleQuestionReportRequest { Status = "Resolved" },
                    question.ExpertId),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Resolved", report.Status);
        Assert.Equal(question.ExpertId, report.ResolvedBy);
        Assert.Equal("Approved", question.Status);
        Assert.True(question.IsActive);
    }

    [Fact]
    public async Task HandleReport_WhenAnotherBlockingReportRemains_KeepsQuestionReported()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "reported-question", "Reported", true);
        var firstReport = await AddReportAsync(database, question.QuestionId, "expert-2", "Expert", "Pending");
        await AddReportAsync(database, question.QuestionId, "admin-1", "Admin", "PendingFix");

        var result = await new HandleQuestionReportCommandHandler(database.Context)
            .Handle(
                new HandleQuestionReportCommand(
                    firstReport.ReportId,
                    new HandleQuestionReportRequest { Status = "Dismissed", ReviewNote = "Không chấp nhận báo cáo này" },
                    question.ExpertId),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Reported", question.Status);
    }

    [Fact]
    public async Task HandleReport_ByNonOwner_ReturnsForbidden()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "non-owner", "Reported", true);
        var report = await AddReportAsync(database, question.QuestionId, "expert-2", "Expert", "Pending");

        var result = await new HandleQuestionReportCommandHandler(database.Context)
            .Handle(
                new HandleQuestionReportCommand(
                    report.ReportId,
                    new HandleQuestionReportRequest { Status = "Resolved" },
                    "expert-3"),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ReportAccessForbidden, result.Error);
    }

    [Fact]
    public async Task HandleReport_WhenStatusIsInvalid_ReturnsValidationError()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "invalid-handle-status", "Reported", true);
        var report = await AddReportAsync(database, question.QuestionId, "expert-2", "Expert", "Pending");

        var result = await new HandleQuestionReportCommandHandler(database.Context)
            .Handle(
                new HandleQuestionReportCommand(
                    report.ReportId,
                    new HandleQuestionReportRequest { Status = "Pending" },
                    question.ExpertId),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ReportStatusInvalid, result.Error);
    }

    [Fact]
    public async Task HandleReport_WhenAlreadyHandled_ReturnsConflict()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "already-handled", "Reported", true);
        var report = await AddReportAsync(database, question.QuestionId, "expert-2", "Expert", "Resolved");

        var result = await new HandleQuestionReportCommandHandler(database.Context)
            .Handle(
                new HandleQuestionReportCommand(
                    report.ReportId,
                    new HandleQuestionReportRequest { Status = "Dismissed" },
                    question.ExpertId),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ReportAlreadyHandled, result.Error);
    }

    [Fact]
    public async Task ResolveStudentReport_WithNewerVersion_TriggersAwardFullAdjustment()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "student-adjustment", "Approved", true);
        var oldVersion = AddVersion(database, question, 1);
        AddVersion(database, question, 2);
        var report = await AddReportAsync(database, question.QuestionId, "student-1", "Student", "Pending");
        report.SessionId = "session-1";
        report.QuestionVersionId = oldVersion.VersionId;
        await database.Context.SaveChangesAsync();
        var adjustment = new RecordingScoreAdjustmentService();

        var result = await new HandleQuestionReportCommandHandler(database.Context, adjustment)
            .Handle(
                new HandleQuestionReportCommand(
                    report.ReportId,
                    new HandleQuestionReportRequest
                    {
                        Status = "Resolved",
                        ResolutionAction = "InvalidateAndAwardFull"
                    },
                    question.ExpertId),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("InvalidateAndAwardFull", report.ResolutionAction);
        Assert.Equal(report.ReportId, adjustment.AdjustedReportId);
    }

    [Fact]
    public async Task ResolveStudentReport_WithoutFix_ReturnsConflictAndDoesNotAdjustScore()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "student-adjustment-no-fix", "Approved", true);
        var version = AddVersion(database, question, 1);
        var report = await AddReportAsync(database, question.QuestionId, "student-1", "Student", "Pending");
        report.SessionId = "session-1";
        report.QuestionVersionId = version.VersionId;
        await database.Context.SaveChangesAsync();
        var adjustment = new RecordingScoreAdjustmentService();

        var result = await new HandleQuestionReportCommandHandler(database.Context, adjustment)
            .Handle(
                new HandleQuestionReportCommand(
                    report.ReportId,
                    new HandleQuestionReportRequest
                    {
                        Status = "Resolved",
                        ResolutionAction = "InvalidateAndAwardFull"
                    },
                    question.ExpertId),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.QuestionFixRequiredBeforeScoreAdjustment, result.Error);
        Assert.Null(adjustment.AdjustedReportId);
        Assert.Equal("Pending", report.Status);
    }

    private static async Task<MathInsight.Shared.Results.Result<ReportQuestionResponse>> CreateReportAsync(
        QuestionBankInMemoryContext database,
        Question question,
        string reporterAccountId,
        string reporterRole,
        string? reason = "The answer needs review.")
    {
        string? sessionId = null;
        string? questionVersionId = null;
        if (string.Equals(reporterRole, "Student", StringComparison.OrdinalIgnoreCase))
        {
            questionVersionId = await database.Context.QuestionVersions
                .Where(version => version.QuestionId == question.QuestionId)
                .OrderByDescending(version => version.VersionNumber)
                .Select(version => version.VersionId)
                .FirstOrDefaultAsync();
            if (questionVersionId is null)
            {
                questionVersionId = AddVersion(database, question, 1).VersionId;
                await database.Context.SaveChangesAsync();
            }

            sessionId = $"session-{reporterAccountId}";
        }

        return await new ReportQuestionCommandHandler(database.Context)
            .Handle(
                new ReportQuestionCommand(
                    question.QuestionId,
                    new ReportQuestionRequest { ReportReason = reason },
                    reporterAccountId,
                    reporterRole,
                    sessionId,
                    questionVersionId),
                CancellationToken.None);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleReport_Dismiss_WhenReviewNoteIsNullOrEmpty_ReturnsValidationError(string? note)
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "dismiss-null-note", "Reported", true);
        var report = await AddReportAsync(database, question.QuestionId, "student-1", "Student", "Pending");

        var result = await new HandleQuestionReportCommandHandler(database.Context)
            .Handle(
                new HandleQuestionReportCommand(
                    report.ReportId,
                    new HandleQuestionReportRequest { Status = "Dismissed", ReviewNote = note },
                    question.ExpertId),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ReviewNoteRequired, result.Error);

        var unchanged = await database.Context.QuestionReports.FindAsync(report.ReportId);
        Assert.NotNull(unchanged);
        Assert.Equal("Pending", unchanged.Status);
        Assert.Null(unchanged.ReviewNote);
    }

    [Fact]
    public async Task HandleReport_Dismiss_WhenReviewNoteExceeds2000Chars_ReturnsValidationError()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "dismiss-overlong-note", "Reported", true);
        var report = await AddReportAsync(database, question.QuestionId, "expert-2", "Expert", "Pending");
        var overlong = new string('x', 2001);

        var result = await new HandleQuestionReportCommandHandler(database.Context)
            .Handle(
                new HandleQuestionReportCommand(
                    report.ReportId,
                    new HandleQuestionReportRequest { Status = "Dismissed", ReviewNote = overlong },
                    question.ExpertId),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ReviewNoteTooLong, result.Error);

        var unchanged = await database.Context.QuestionReports.FindAsync(report.ReportId);
        Assert.NotNull(unchanged);
        Assert.Equal("Pending", unchanged.Status);
    }

    [Fact]
    public async Task HandleReport_DismissStudentReport_WithValidNote_TrimmedAndPersistedWithoutQuestionMutation()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var adjustment = new RecordingScoreAdjustmentService();
        var question = await AddQuestionAsync(database, "dismiss-student-success", "Reported", true);
        var report = await AddReportAsync(database, question.QuestionId, "student-1", "Student", "Pending");
        var originalContent = question.QuestionContent;
        var initialVersionCount = await database.Context.QuestionVersions.CountAsync(v => v.QuestionId == question.QuestionId);

        var result = await new HandleQuestionReportCommandHandler(database.Context, adjustment)
            .Handle(
                new HandleQuestionReportCommand(
                    report.ReportId,
                    new HandleQuestionReportRequest
                    {
                        Status = "Dismissed",
                        ReviewNote = "  Câu hỏi hoàn toàn đúng định dạng và đáp án.  "
                    },
                    question.ExpertId),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Dismissed", result.Value.Status);
        Assert.Equal("NoScoreChange", result.Value.ResolutionAction);
        Assert.Equal("Câu hỏi hoàn toàn đúng định dạng và đáp án.", result.Value.ReviewNote);

        // Verify direct DB persistence
        var persisted = await database.Context.QuestionReports.FindAsync(report.ReportId);
        Assert.NotNull(persisted);
        Assert.Equal("Dismissed", persisted.Status);
        Assert.Equal("NoScoreChange", persisted.ResolutionAction);
        Assert.Equal("Câu hỏi hoàn toàn đúng định dạng và đáp án.", persisted.ReviewNote);
        Assert.Equal(question.ExpertId, persisted.ResolvedBy);
        Assert.NotNull(persisted.ResolvedTime);

        // Verify question content not mutated, no new version created, no score adjustment called
        var persistedQuestion = await database.Context.Questions.FindAsync(question.QuestionId);
        Assert.NotNull(persistedQuestion);
        Assert.Equal(originalContent, persistedQuestion.QuestionContent);
        var finalVersionCount = await database.Context.QuestionVersions.CountAsync(v => v.QuestionId == question.QuestionId);
        Assert.Equal(initialVersionCount, finalVersionCount);
        Assert.Null(adjustment.AdjustedReportId);

        // Verify read back via GetQuestionReportsQueryHandler
        var queryResult = await new GetQuestionReportsQueryHandler(database.Context)
            .Handle(new GetQuestionReportsQuery(question.QuestionId, question.ExpertId, "Dismissed"), CancellationToken.None);
        Assert.True(queryResult.IsSuccess);
        var reportedItem = Assert.Single(queryResult.Value);
        Assert.Equal("Câu hỏi hoàn toàn đúng định dạng và đáp án.", reportedItem.ReviewNote);
    }

    [Fact]
    public async Task HandleReport_DismissExpertReport_WithValidNote_SucceedsAndPersists()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "dismiss-expert-success", "Reported", true);
        var report = await AddReportAsync(database, question.QuestionId, "expert-2", "Expert", "Pending");

        var result = await new HandleQuestionReportCommandHandler(database.Context)
            .Handle(
                new HandleQuestionReportCommand(
                    report.ReportId,
                    new HandleQuestionReportRequest
                    {
                        Status = "Dismissed",
                        ReviewNote = "Nội dung kiến thức lớp 12 hoàn toàn phù hợp chương trình."
                    },
                    question.ExpertId),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Dismissed", result.Value.Status);
        Assert.Equal("NoScoreChange", result.Value.ResolutionAction);
        Assert.Equal("Nội dung kiến thức lớp 12 hoàn toàn phù hợp chương trình.", result.Value.ReviewNote);

        var persisted = await database.Context.QuestionReports.FindAsync(report.ReportId);
        Assert.NotNull(persisted);
        Assert.Equal("Dismissed", persisted.Status);
        Assert.Equal("Nội dung kiến thức lớp 12 hoàn toàn phù hợp chương trình.", persisted.ReviewNote);
    }

    [Fact]
    public async Task HandleReport_Dismiss_WhenBelongsToIncident_ReturnsIncidentSubmissionRequired()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "dismiss-incident-report", "Reported", true);
        var report = await AddReportAsync(database, question.QuestionId, "student-1", "Student", "Pending");
        report.IncidentId = "inc-existing-123";
        await database.Context.SaveChangesAsync();

        var result = await new HandleQuestionReportCommandHandler(database.Context)
            .Handle(
                new HandleQuestionReportCommand(
                    report.ReportId,
                    new HandleQuestionReportRequest
                    {
                        Status = "Dismissed",
                        ReviewNote = "Dismiss attempt directly"
                    },
                    question.ExpertId),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ReportIncidentSubmissionRequired, result.Error);
    }

    [Fact]
    public async Task HandleReport_Resolve_DoesNotClearExistingReviewNote()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "resolve-keep-note", "Approved", true);
        var oldVersion = AddVersion(database, question, 1);
        AddVersion(database, question, 2);

        var report = await AddReportAsync(database, question.QuestionId, "student-1", "Student", "Pending");
        report.SessionId = "session-1";
        report.QuestionVersionId = oldVersion.VersionId;
        report.ReviewNote = "Existing note from previous admin feedback";
        await database.Context.SaveChangesAsync();

        var adjustment = new RecordingScoreAdjustmentService();
        var result = await new HandleQuestionReportCommandHandler(database.Context, adjustment)
            .Handle(
                new HandleQuestionReportCommand(
                    report.ReportId,
                    new HandleQuestionReportRequest
                    {
                        Status = "Resolved",
                        ResolutionAction = "InvalidateAndAwardFull",
                        ReviewNote = null
                    },
                    question.ExpertId),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Resolved", result.Value.Status);

        var persisted = await database.Context.QuestionReports.FindAsync(report.ReportId);
        Assert.NotNull(persisted);
        Assert.Equal("Existing note from previous admin feedback", persisted.ReviewNote);
    }

    private static async Task<Question> AddQuestionAsync(
        QuestionBankInMemoryContext database,
        string questionId,
        string status,
        bool isActive)
    {
        var difficulty = new TagDifficulty
        {
            DifficultyId = $"difficulty-{questionId}",
            DifficultyName = $"Difficulty {questionId}",
            LevelValue = 1,
            DisplayOrder = 1,
            IsActive = true
        };

        var question = new Question
        {
            QuestionId = questionId,
            QuestionContent = "Question content",
            SolutionContent = "Solution content",
            DifficultyId = difficulty.DifficultyId,
            Difficulty = difficulty,
            Grade = 10,
            Status = status,
            QuestionType = "SingleChoice",
            ExpertId = "expert-1",
            DefaultWeight = 1m,
            IsActive = isActive
        };

        database.Context.Questions.Add(question);
        await database.Context.SaveChangesAsync();
        return question;
    }

    private static async Task<QuestionReport> AddReportAsync(
        QuestionBankInMemoryContext database,
        string questionId,
        string reporterAccountId,
        string reporterRole,
        string status)
    {
        var report = new QuestionReport
        {
            ReportId = Guid.NewGuid().ToString(),
            QuestionId = questionId,
            ReporterAccountId = reporterAccountId,
            ReporterRole = reporterRole,
            ReportReason = "Needs review.",
            Status = status,
            CreatedTime = DateTime.UtcNow
        };

        database.Context.QuestionReports.Add(report);
        await database.Context.SaveChangesAsync();
        return report;
    }

    private static QuestionVersion AddVersion(
        QuestionBankInMemoryContext database,
        Question question,
        int versionNumber)
    {
        var version = new QuestionVersion
        {
            VersionId = $"{question.QuestionId}-v{versionNumber}",
            QuestionId = question.QuestionId,
            QuestionContent = question.QuestionContent,
            QuestionAnswer = question.SolutionContent,
            AnswersSnapshot = "{}",
            VersionNumber = versionNumber,
            SnapshotSchemaVersion = 2,
            CreatedTime = DateTime.UtcNow.AddMinutes(versionNumber),
            ExpertId = question.ExpertId
        };
        database.Context.QuestionVersions.Add(version);
        return version;
    }

    private static async Task AddDirectChildTopicAsync(
        QuestionBankInMemoryContext database,
        string topicId,
        int grade)
    {
        var root = new TagTopic
        {
            TagId = $"{topicId}-root",
            TagName = "Root topic",
            Grade = grade,
            DisplayOrder = 1,
            IsActive = true
        };
        database.Context.TagTopics.Add(root);
        database.Context.TagTopics.Add(new TagTopic
        {
            TagId = topicId,
            ParentTagId = root.TagId,
            TagName = "Direct child topic",
            Grade = grade,
            DisplayOrder = 1,
            IsActive = true
        });
        await database.Context.SaveChangesAsync();
    }

    private static UpdateQuestionRequest CreateUpdateRequest(Question question) => new()
    {
        QuestionContent = "Updated question content",
        SolutionContent = "Updated solution content",
        DifficultyId = question.DifficultyId,
        Grade = question.Grade,
        QuestionType = "SINGLE_CHOICE",
        DefaultWeight = 1m,
        Topics =
        [
            new CreateQuestionTopicRequest("topic-1", true)
        ],
        Answers =
        [
            new CreateAnswerRequest { AnswerContent = "A", IsCorrect = true },
            new CreateAnswerRequest { AnswerContent = "B", IsCorrect = false }
        ]
    };

    private sealed class RecordingScoreAdjustmentService : IScoreAdjustmentService
    {
        public string? AdjustedReportId { get; private set; }

        public Task AdjustInvalidQuestionVersionAsync(
            string reportId,
            CancellationToken cancellationToken = default)
        {
            AdjustedReportId = reportId;
            return Task.CompletedTask;
        }

        public Task DispatchPendingAdjustmentsAsync(
            string? reportId = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RecoverPendingAdjustmentsAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
