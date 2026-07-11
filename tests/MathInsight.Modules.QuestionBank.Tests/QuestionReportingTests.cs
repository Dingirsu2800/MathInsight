using MathInsight.Modules.QuestionBank.Commands.HandleQuestionReport;
using MathInsight.Modules.QuestionBank.Commands.ReportQuestion;
using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Modules.QuestionBank.Errors;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Tests;

public sealed class QuestionReportingTests
{
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
        await AddReportAsync(database, question.QuestionId, "admin-1", "Admin", "Pending");

        var result = await new HandleQuestionReportCommandHandler(database.Context)
            .Handle(
                new HandleQuestionReportCommand(
                    firstReport.ReportId,
                    new HandleQuestionReportRequest { Status = "Dismissed" },
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

    private static Task<MathInsight.Shared.Results.Result<ReportQuestionResponse>> CreateReportAsync(
        QuestionBankInMemoryContext database,
        Question question,
        string reporterAccountId,
        string reporterRole,
        string? reason = "The answer needs review.")
    {
        return new ReportQuestionCommandHandler(database.Context)
            .Handle(
                new ReportQuestionCommand(
                    question.QuestionId,
                    new ReportQuestionRequest { ReportReason = reason },
                    reporterAccountId,
                    reporterRole),
                CancellationToken.None);
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
            DefaultPoint = 1m,
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
}
