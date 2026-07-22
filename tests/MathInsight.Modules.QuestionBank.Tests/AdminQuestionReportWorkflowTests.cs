using MathInsight.Modules.QuestionBank.Commands.AdminApproveQuestionReport;
using MathInsight.Modules.QuestionBank.Commands.AdminRejectQuestionReport;
using MathInsight.Modules.QuestionBank.Commands.HandleQuestionReport;
using MathInsight.Modules.QuestionBank.Commands.ReportQuestion;
using MathInsight.Modules.QuestionBank.Commands.SubmitQuestionReportReview;
using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Queries.GetAdminQuestionReports;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Tests;

public sealed class AdminQuestionReportWorkflowTests
{
    [Fact]
    public async Task AdminReport_CreatesPendingFixAndMovesQuestionToReported()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "admin-create", "Approved");

        var result = await CreateReportAsync(database, question, "admin-1", "Admin");

        Assert.True(result.IsSuccess);
        Assert.Equal("PendingFix", result.Value!.Status);
        Assert.Equal("Reported", question.Status);
    }

    [Fact]
    public async Task AdminReport_WhenActiveWorkflowExists_ReturnsWorkflowConflict()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "admin-duplicate", "Approved");

        Assert.True((await CreateReportAsync(database, question, "admin-1", "Admin")).IsSuccess);
        var duplicate = await CreateReportAsync(database, question, "admin-2", "Admin");

        Assert.True(duplicate.IsFailure);
        Assert.Equal(QuestionBankErrors.AdminReportWorkflowAlreadyExists, duplicate.Error);
    }

    [Fact]
    public async Task AdminReport_WhenRejectedQuestionHasActiveWorkflow_ReturnsWorkflowConflict()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "admin-rejected-duplicate", "Rejected");
        await AddReportAsync(database, question.QuestionId, "admin-1", "Admin", "PendingFix");

        var result = await CreateReportAsync(database, question, "admin-2", "Admin");

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.AdminReportWorkflowAlreadyExists, result.Error);
    }

    [Fact]
    public async Task ExpertOwner_SubmitReview_MovesPendingFixToPendingReview()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "submit-review", "Reported");
        var report = await AddReportAsync(database, question.QuestionId, "admin-1", "Admin", "PendingFix");

        var result = await new SubmitQuestionReportReviewCommandHandler(database.Context)
            .Handle(new SubmitQuestionReportReviewCommand(report.ReportId, question.ExpertId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("PendingReview", report.Status);
        Assert.NotNull(report.SubmittedTime);
        Assert.Equal("Reported", question.Status);
    }

    [Fact]
    public async Task ExpertWhoDoesNotOwnQuestion_CannotSubmitReview()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "submit-forbidden", "Reported");
        var report = await AddReportAsync(database, question.QuestionId, "admin-1", "Admin", "PendingFix");

        var result = await new SubmitQuestionReportReviewCommandHandler(database.Context)
            .Handle(new SubmitQuestionReportReviewCommand(report.ReportId, "expert-2"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ReportAccessForbidden, result.Error);
    }

    [Fact]
    public async Task ExpertCannotResolveAdminReportThroughRegularEndpoint()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "admin-regular-handle", "Reported");
        var report = await AddReportAsync(database, question.QuestionId, "admin-1", "Admin", "PendingFix");

        var result = await new HandleQuestionReportCommandHandler(database.Context)
            .Handle(
                new HandleQuestionReportCommand(
                    report.ReportId,
                    new HandleQuestionReportRequest { Status = "Resolved" },
                    question.ExpertId),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.AdminReportRequiresReview, result.Error);
    }

    [Fact]
    public async Task OriginalAdminRejectsThenExpertCanSubmitAgain()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "reject-resubmit", "Reported");
        var report = await AddReportAsync(database, question.QuestionId, "admin-1", "Admin", "PendingReview");

        var reject = await new AdminRejectQuestionReportCommandHandler(database.Context)
            .Handle(
                new AdminRejectQuestionReportCommand(
                    report.ReportId,
                    new AdminRejectQuestionReportRequest { ReviewNote = "Formula still needs correction." },
                    "admin-1"),
                CancellationToken.None);

        Assert.True(reject.IsSuccess);
        Assert.Equal("Rejected", question.Status);
        Assert.Equal("Formula still needs correction.", report.ReviewNote);

        var submit = await new SubmitQuestionReportReviewCommandHandler(database.Context)
            .Handle(new SubmitQuestionReportReviewCommand(report.ReportId, question.ExpertId), CancellationToken.None);

        Assert.True(submit.IsSuccess);
        Assert.Equal("PendingReview", report.Status);
        Assert.Equal("Reported", question.Status);
    }

    [Fact]
    public async Task AnotherAdminCannotApproveOriginalAdminReport()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "approve-forbidden", "Reported");
        var report = await AddReportAsync(database, question.QuestionId, "admin-1", "Admin", "PendingReview");

        var result = await new AdminApproveQuestionReportCommandHandler(database.Context)
            .Handle(new AdminApproveQuestionReportCommand(report.ReportId, "admin-2"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ReportAccessForbidden, result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AdminReject_RequiresReviewNote(string reviewNote)
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "reject-note-required", "Reported");
        var report = await AddReportAsync(database, question.QuestionId, "admin-1", "Admin", "PendingReview");

        var result = await new AdminRejectQuestionReportCommandHandler(database.Context)
            .Handle(
                new AdminRejectQuestionReportCommand(
                    report.ReportId,
                    new AdminRejectQuestionReportRequest { ReviewNote = reviewNote },
                    "admin-1"),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ReviewNoteRequired, result.Error);
    }

    [Fact]
    public async Task AdminApprove_KeepsQuestionReportedWhenPendingExpertReportRemains()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "approve-blocked", "Reported");
        var adminReport = await AddReportAsync(database, question.QuestionId, "admin-1", "Admin", "PendingReview");
        await AddReportAsync(database, question.QuestionId, "expert-2", "Expert", "Pending");
        await AddReportAsync(database, question.QuestionId, "student-1", "Student", "Pending");

        var result = await new AdminApproveQuestionReportCommandHandler(database.Context)
            .Handle(new AdminApproveQuestionReportCommand(adminReport.ReportId, "admin-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Resolved", adminReport.Status);
        Assert.Equal("admin-1", adminReport.ReviewedBy);
        Assert.Equal("Reported", question.Status);
    }

    [Fact]
    public async Task AdminApprove_WithOnlyStudentReport_RestoresQuestionToApproved()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "approve-happy-path", "Reported");
        var adminReport = await AddReportAsync(database, question.QuestionId, "admin-1", "Admin", "PendingReview");
        await AddReportAsync(database, question.QuestionId, "student-1", "Student", "Pending");

        var result = await new AdminApproveQuestionReportCommandHandler(database.Context)
            .Handle(new AdminApproveQuestionReportCommand(adminReport.ReportId, "admin-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Resolved", adminReport.Status);
        Assert.Equal("Approved", question.Status);
        Assert.NotNull(adminReport.ResolvedTime);
        Assert.Equal("admin-1", adminReport.ResolvedBy);
    }

    [Fact]
    public async Task AdminReject_WhenReviewNoteExceedsLimit_ReturnsValidationError()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "reject-note-too-long", "Reported");
        var report = await AddReportAsync(database, question.QuestionId, "admin-1", "Admin", "PendingReview");

        var result = await new AdminRejectQuestionReportCommandHandler(database.Context)
            .Handle(
                new AdminRejectQuestionReportCommand(
                    report.ReportId,
                    new AdminRejectQuestionReportRequest { ReviewNote = new string('x', 2001) },
                    "admin-1"),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ReviewNoteTooLong, result.Error);
    }

    [Fact]
    public async Task AdminApprove_WhenReportIsNotPendingReview_ReturnsConflict()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "approve-invalid-state", "Reported");
        var report = await AddReportAsync(database, question.QuestionId, "admin-1", "Admin", "PendingFix");

        var result = await new AdminApproveQuestionReportCommandHandler(database.Context)
            .Handle(new AdminApproveQuestionReportCommand(report.ReportId, "admin-1"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.ReportAlreadyHandled, result.Error);
    }

    [Fact]
    public async Task AdminReportsMine_ReturnsOnlyCurrentAdminAndRequestedStatus()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var firstQuestion = await AddQuestionAsync(database, "admin-query-1", "Reported");
        var secondQuestion = await AddQuestionAsync(database, "admin-query-2", "Reported");
        await AddReportAsync(database, firstQuestion.QuestionId, "admin-1", "Admin", "PendingReview");
        await AddReportAsync(database, secondQuestion.QuestionId, "admin-2", "Admin", "PendingReview");
        await AddReportAsync(database, firstQuestion.QuestionId, "admin-1", "Admin", "Resolved");

        var result = await new GetAdminQuestionReportsQueryHandler(database.Context)
            .Handle(new GetAdminQuestionReportsQuery("admin-1", "PendingReview", 1, 10), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Items);
        Assert.Equal(firstQuestion.QuestionId, result.Value.Items[0].QuestionId);
    }

    private static Task<MathInsight.Shared.Results.Result<ReportQuestionResponse>> CreateReportAsync(
        QuestionBankInMemoryContext database,
        Question question,
        string reporterAccountId,
        string reporterRole)
    {
        return new ReportQuestionCommandHandler(database.Context)
            .Handle(
                new ReportQuestionCommand(
                    question.QuestionId,
                    new ReportQuestionRequest { ReportReason = "Needs review." },
                    reporterAccountId,
                    reporterRole),
                CancellationToken.None);
    }

    private static async Task<Question> AddQuestionAsync(
        QuestionBankInMemoryContext database,
        string questionId,
        string status)
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
            IsActive = true
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
}
