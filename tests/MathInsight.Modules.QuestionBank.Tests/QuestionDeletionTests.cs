using MathInsight.Modules.QuestionBank.Commands.DeleteQuestion;
using MathInsight.Modules.QuestionBank.Commands.ToggleQuestionActive;
using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Modules.QuestionBank.Errors;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Tests;

public sealed class QuestionDeletionTests
{
    [Fact]
    public async Task DeleteQuestion_WhenUnused_HardDeletesQuestion()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "question-unused");

        var result = await new DeleteQuestionCommandHandler(database.Context)
            .Handle(new DeleteQuestionCommand(question.QuestionId, question.ExpertId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("HardDeleted", result.Value!.DeleteMode);
        Assert.False(await database.Context.Questions.AnyAsync(item => item.QuestionId == question.QuestionId));
    }

    [Fact]
    public async Task DeleteQuestion_WhenUsedInTest_ReturnsQuestionInUseAndKeepsQuestionUnchanged()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "question-used");
        database.Context.TestQuestionReadModels.Add(new TestQuestionReadModel
        {
            TestId = "test-1",
            QuestionId = question.QuestionId
        });
        await database.Context.SaveChangesAsync();

        var result = await new DeleteQuestionCommandHandler(database.Context)
            .Handle(new DeleteQuestionCommand(question.QuestionId, question.ExpertId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.QuestionInUse, result.Error);

        var persistedQuestion = await database.Context.Questions.SingleAsync(item => item.QuestionId == question.QuestionId);
        Assert.True(persistedQuestion.IsActive);
        Assert.Equal("Approved", persistedQuestion.Status);
    }

    [Fact]
    public async Task DeleteQuestion_WhenPendingReportExists_ReturnsConflictAndPreservesReportAudit()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "question-pending-report");
        database.Context.QuestionReports.Add(new QuestionReport
        {
            ReportId = "report-pending",
            QuestionId = question.QuestionId,
            ReporterAccountId = "student-1",
            ReporterRole = "Student",
            ReportReason = "Needs review.",
            Status = "Pending",
            CreatedTime = DateTime.UtcNow
        });
        await database.Context.SaveChangesAsync();

        var result = await new DeleteQuestionCommandHandler(database.Context)
            .Handle(new DeleteQuestionCommand(question.QuestionId, question.ExpertId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.QuestionHasPendingReports, result.Error);
        Assert.True(await database.Context.Questions.AnyAsync(item => item.QuestionId == question.QuestionId));
        Assert.True(await database.Context.QuestionReports.AnyAsync(item => item.QuestionId == question.QuestionId));
    }

    [Theory]
    [InlineData("PendingFix")]
    [InlineData("PendingReview")]
    public async Task DeleteQuestion_WhenAdminWorkflowIsActive_ReturnsConflict(string reportStatus)
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, $"question-{reportStatus}");
        database.Context.QuestionReports.Add(new QuestionReport
        {
            ReportId = $"report-{reportStatus}",
            QuestionId = question.QuestionId,
            ReporterAccountId = "admin-1",
            ReporterRole = "Admin",
            ReportReason = "Needs review.",
            Status = reportStatus,
            CreatedTime = DateTime.UtcNow
        });
        await database.Context.SaveChangesAsync();

        var result = await new DeleteQuestionCommandHandler(database.Context)
            .Handle(new DeleteQuestionCommand(question.QuestionId, question.ExpertId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.QuestionHasPendingReports, result.Error);
    }

    [Fact]
    public async Task DeleteQuestion_WhenReportedWithoutPendingReport_ReturnsConflict()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "question-reported");
        question.Status = "Reported";
        await database.Context.SaveChangesAsync();

        var result = await new DeleteQuestionCommandHandler(database.Context)
            .Handle(new DeleteQuestionCommand(question.QuestionId, question.ExpertId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.QuestionHasPendingReports, result.Error);
    }

    [Fact]
    public async Task ToggleQuestionActive_WhenUsedInTest_ReturnsQuestionInUseAndKeepsQuestionActive()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "question-toggle");
        database.Context.TestQuestionReadModels.Add(new TestQuestionReadModel
        {
            TestId = "test-2",
            QuestionId = question.QuestionId
        });
        await database.Context.SaveChangesAsync();

        var result = await new ToggleQuestionActiveCommandHandler(database.Context)
            .Handle(new ToggleQuestionActiveCommand(question.QuestionId, false, question.ExpertId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.QuestionInUse, result.Error);
        Assert.True(question.IsActive);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("PendingFix")]
    [InlineData("PendingReview")]
    public async Task ToggleQuestionActive_WhenActiveReportExists_ReturnsConflictAndKeepsQuestionActive(string reportStatus)
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, $"question-toggle-{reportStatus}");
        database.Context.QuestionReports.Add(new QuestionReport
        {
            ReportId = $"report-toggle-{reportStatus}",
            QuestionId = question.QuestionId,
            ReporterAccountId = reportStatus == "Pending" ? "expert-2" : "admin-1",
            ReporterRole = reportStatus == "Pending" ? "Expert" : "Admin",
            ReportReason = "Needs review.",
            Status = reportStatus,
            CreatedTime = DateTime.UtcNow
        });
        await database.Context.SaveChangesAsync();

        var result = await new ToggleQuestionActiveCommandHandler(database.Context)
            .Handle(new ToggleQuestionActiveCommand(question.QuestionId, false, question.ExpertId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(QuestionBankErrors.QuestionHasPendingReports, result.Error);
        Assert.True(question.IsActive);
        Assert.Equal("Approved", question.Status);
    }

    private static async Task<Question> AddQuestionAsync(QuestionBankInMemoryContext database, string questionId)
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
            Status = "Approved",
            QuestionType = "SingleChoice",
            ExpertId = "expert-1",
            DefaultPoint = 1m,
            IsActive = true
        };

        database.Context.Questions.Add(question);
        await database.Context.SaveChangesAsync();

        return question;
    }
}
