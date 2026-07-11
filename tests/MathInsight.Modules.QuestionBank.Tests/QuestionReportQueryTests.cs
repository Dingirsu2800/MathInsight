using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Queries.GetOwnedReportedQuestions;
using MathInsight.Modules.QuestionBank.Queries.GetQuestionReports;

namespace MathInsight.Modules.QuestionBank.Tests;

public sealed class QuestionReportQueryTests
{
    [Fact]
    public async Task OwnedReportedQuestions_ReturnsOnlyOwnerQuestions_GroupedAndPaged()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var firstQuestion = await AddQuestionAsync(database, "first-question", "expert-1");
        var secondQuestion = await AddQuestionAsync(database, "second-question", "expert-1");
        var otherQuestion = await AddQuestionAsync(database, "other-question", "expert-2");

        await AddReportAsync(database, firstQuestion.QuestionId, "student-1", "Student", "Pending", DateTime.UtcNow.AddMinutes(-10));
        await AddReportAsync(database, secondQuestion.QuestionId, "expert-3", "Expert", "Pending", DateTime.UtcNow.AddMinutes(-1));
        await AddReportAsync(database, secondQuestion.QuestionId, "admin-1", "Admin", "PendingFix", DateTime.UtcNow);
        await AddReportAsync(database, otherQuestion.QuestionId, "student-2", "Student", "Pending", DateTime.UtcNow.AddMinutes(1));

        var result = await new GetOwnedReportedQuestionsQueryHandler(database.Context)
            .Handle(new GetOwnedReportedQuestionsQuery("expert-1", "Pending", 1, 1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.TotalCount);
        Assert.Equal(2, result.Value.TotalPages);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(secondQuestion.QuestionId, item.QuestionId);
        Assert.Equal(2, item.PendingReportCount);
        Assert.Contains("Expert", item.ReporterRoles);
        Assert.Contains("Admin", item.ReporterRoles);
    }

    [Fact]
    public async Task QuestionReports_PendingFilterIncludesAdminWorkflowStatuses()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "admin-workflow-detail", "expert-1");
        await AddReportAsync(database, question.QuestionId, "expert-2", "Expert", "Pending", DateTime.UtcNow.AddMinutes(-1));
        await AddReportAsync(database, question.QuestionId, "admin-1", "Admin", "PendingReview", DateTime.UtcNow);

        var result = await new GetQuestionReportsQueryHandler(database.Context)
            .Handle(new GetQuestionReportsQuery(question.QuestionId, "expert-1", "Pending"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.Contains(result.Value, report => report.Status == "PendingReview" && report.ReporterRole == "Admin");
    }

    [Fact]
    public async Task QuestionReports_ReturnsNewestFirstOnlyForOwner()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "reported-question", "expert-1");
        database.Context.AccountReadModels.Add(new AccountReadModel
        {
            AccountId = "student-1",
            Username = "student",
            Email = "student@example.test",
            FirstName = "Student",
            LastName = "One"
        });
        await database.Context.SaveChangesAsync();

        await AddReportAsync(database, question.QuestionId, "student-1", "Student", "Pending", DateTime.UtcNow.AddMinutes(-1));
        var newest = await AddReportAsync(database, question.QuestionId, "expert-2", "Expert", "Pending", DateTime.UtcNow);

        var handler = new GetQuestionReportsQueryHandler(database.Context);
        var ownerResult = await handler.Handle(
            new GetQuestionReportsQuery(question.QuestionId, "expert-1", "Pending"),
            CancellationToken.None);
        var nonOwnerResult = await handler.Handle(
            new GetQuestionReportsQuery(question.QuestionId, "expert-3", "Pending"),
            CancellationToken.None);

        Assert.True(ownerResult.IsSuccess);
        Assert.Equal(newest.ReportId, ownerResult.Value![0].ReportId);
        Assert.Equal("Student One", ownerResult.Value[1].ReporterName);
        Assert.True(nonOwnerResult.IsFailure);
        Assert.Equal(QuestionBankErrors.ReportAccessForbidden, nonOwnerResult.Error);
    }

    private static async Task<Question> AddQuestionAsync(
        QuestionBankInMemoryContext database,
        string questionId,
        string expertId)
    {
        var difficulty = new TagDifficulty
        {
            DifficultyId = $"difficulty-{questionId}",
            DifficultyName = $"Difficulty {questionId}",
            LevelValue = database.Context.TagDifficulties.Count() + 1,
            DisplayOrder = 1,
            IsActive = true
        };

        var topic = new TagTopic
        {
            TagId = $"topic-{questionId}",
            TagName = $"Topic {questionId}",
            Grade = 10,
            DisplayOrder = 1,
            IsActive = true
        };

        var question = new Question
        {
            QuestionId = questionId,
            QuestionContent = $"Content {questionId}",
            SolutionContent = "Solution",
            DifficultyId = difficulty.DifficultyId,
            Difficulty = difficulty,
            Grade = 10,
            Status = "Reported",
            QuestionType = "SingleChoice",
            ExpertId = expertId,
            DefaultPoint = 1m,
            IsActive = true,
            QuestionTopics = new List<QuestionTopic>
            {
                new()
                {
                    QuestionTopicId = Guid.NewGuid().ToString(),
                    TagId = topic.TagId,
                    Tag = topic,
                    IsPrimary = true
                }
            }
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
        string status,
        DateTime createdTime)
    {
        var report = new QuestionReport
        {
            ReportId = Guid.NewGuid().ToString(),
            QuestionId = questionId,
            ReporterAccountId = reporterAccountId,
            ReporterRole = reporterRole,
            ReportReason = $"Reason {reporterAccountId}",
            Status = status,
            CreatedTime = createdTime
        };

        database.Context.QuestionReports.Add(report);
        await database.Context.SaveChangesAsync();
        return report;
    }
}
