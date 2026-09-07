using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Queries.GetOwnedReportedQuestions;
using MathInsight.Modules.QuestionBank.Queries.GetQuestionReports;
using MathInsight.Modules.QuestionBank.Queries.GetQuestionReportIncident;
using MathInsight.Modules.QuestionBank.Queries.GetQuestionDetail;
using Microsoft.EntityFrameworkCore;

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
        var latestReport = await AddReportAsync(database, secondQuestion.QuestionId, "admin-1", "Admin", "PendingFix", DateTime.UtcNow);
        await AddReportAsync(database, otherQuestion.QuestionId, "student-2", "Student", "Pending", DateTime.UtcNow.AddMinutes(1));
        latestReport.IncidentId = "incident-second-question";
        database.Context.QuestionReportIncidents.Add(new QuestionReportIncident
        {
            IncidentId = latestReport.IncidentId,
            QuestionId = secondQuestion.QuestionId,
            QuestionVersionId = "version-second-question",
            Status = "Open",
            CreatedTime = DateTime.UtcNow,
            UpdatedTime = DateTime.UtcNow
        });
        await database.Context.SaveChangesAsync();

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
        Assert.Equal(["Pending", "PendingFix"], item.ActiveReportStatuses);
        Assert.Equal("incident-second-question", item.IncidentId);
    }

    [Fact]
    public async Task OwnedReportedQuestions_ReturnsDistinctActiveStatusesForEachQuestion()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "mixed-active-statuses", "expert-1");

        await AddReportAsync(database, question.QuestionId, "student-1", "Student", "Pending", DateTime.UtcNow.AddMinutes(-2));
        await AddReportAsync(database, question.QuestionId, "admin-1", "Admin", "PendingFix", DateTime.UtcNow.AddMinutes(-1));
        await AddReportAsync(database, question.QuestionId, "admin-2", "Admin", "PendingReview", DateTime.UtcNow);

        var result = await new GetOwnedReportedQuestionsQueryHandler(database.Context)
            .Handle(new GetOwnedReportedQuestionsQuery("expert-1", "PendingReview", 1, 10), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal(question.QuestionId, item.QuestionId);
        Assert.Equal(["Pending", "PendingFix", "PendingReview"], item.ActiveReportStatuses);
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

    [Fact]
    public async Task IncidentDetail_ReturnsOriginalVersionAndAllProposedReportDecisionsToOwnerAndAssignedAdmin()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "incident-detail-question", "expert-1");
        var version = new QuestionVersion
        {
            VersionId = "incident-detail-version",
            QuestionId = question.QuestionId,
            QuestionContent = question.QuestionContent,
            QuestionAnswer = "A",
            AnswersSnapshot = "[]",
            VersionNumber = 1,
            CreatedTime = DateTime.UtcNow,
            ExpertId = question.ExpertId
        };
        var incident = new QuestionReportIncident
        {
            IncidentId = "incident-detail-id",
            QuestionId = question.QuestionId,
            QuestionVersionId = version.VersionId,
            Status = "PendingAdminReview",
            RequiresAdminReview = true,
            AssignedAdminId = "admin-1",
            SubmittedCorrectionVersionId = "incident-detail-correction",
            ProposedResolutionAction = "InvalidateAndAwardFull",
            Revision = 4,
            CreatedTime = DateTime.UtcNow,
            UpdatedTime = DateTime.UtcNow
        };
        database.Context.QuestionVersions.Add(version);
        database.Context.QuestionReportIncidents.Add(incident);
        var studentReport = await AddReportAsync(database, question.QuestionId, "student-1", "Student", "Pending", DateTime.UtcNow.AddMinutes(-1));
        studentReport.IncidentId = incident.IncidentId;
        studentReport.QuestionVersionId = version.VersionId;
        studentReport.ProposedStatus = "Resolved";
        var adminReport = await AddReportAsync(database, question.QuestionId, "admin-1", "Admin", "PendingReview", DateTime.UtcNow);
        adminReport.IncidentId = incident.IncidentId;
        adminReport.QuestionVersionId = version.VersionId;
        adminReport.ProposedStatus = "Dismissed";
        adminReport.ReviewNote = "Bản sửa cần kiểm tra lại trước khi duyệt.";
        await database.Context.SaveChangesAsync();

        var handler = new GetQuestionReportIncidentQueryHandler(database.Context);
        var ownerResult = await handler.Handle(
            new GetQuestionReportIncidentQuery(incident.IncidentId, "expert-1", "Expert"),
            CancellationToken.None);
        var adminResult = await handler.Handle(
            new GetQuestionReportIncidentQuery(incident.IncidentId, "admin-1", "Admin"),
            CancellationToken.None);
        var otherAdmin = await handler.Handle(
            new GetQuestionReportIncidentQuery(incident.IncidentId, "admin-2", "Admin"),
            CancellationToken.None);

        Assert.True(ownerResult.IsSuccess);
        Assert.Equal(incident.Revision, ownerResult.Value!.Revision);
        Assert.Equal(version.VersionId, ownerResult.Value.OriginalVersion.VersionId);
        Assert.Equal(2, ownerResult.Value.Reports.Count);
        Assert.Contains(ownerResult.Value.Reports, item => item.ReportId == studentReport.ReportId && item.ProposedStatus == "Resolved");
        Assert.Contains(ownerResult.Value.Reports, item =>
            item.ReportId == adminReport.ReportId &&
            item.ReviewNote == "Bản sửa cần kiểm tra lại trước khi duyệt.");
        Assert.True(adminResult.IsSuccess);
        Assert.Equal("InvalidateAndAwardFull", adminResult.Value!.ProposedResolutionAction);
        Assert.True(otherAdmin.IsFailure);
        Assert.Equal(QuestionBankErrors.ReportAccessForbidden, otherAdmin.Error);
    }

    [Fact]
    public async Task QuestionDetail_ReturnsEligibilityForCurrentExpertAndAdminReporter()
    {
        await using var database = await QuestionBankInMemoryContext.CreateAsync();
        var question = await AddQuestionAsync(database, "detail-eligibility-question", "expert-1");
        var version = new QuestionVersion
        {
            VersionId = "detail-eligibility-v1",
            QuestionId = question.QuestionId,
            QuestionContent = question.QuestionContent,
            QuestionAnswer = "A",
            AnswersSnapshot = "[]",
            VersionNumber = 1,
            CreatedTime = DateTime.UtcNow,
            ExpertId = question.ExpertId
        };
        database.Context.QuestionVersions.Add(version);
        await database.Context.SaveChangesAsync();
        await AddReportAsync(database, question.QuestionId, "expert-2", "Expert", "Pending", DateTime.UtcNow);
        var report = await database.Context.QuestionReports.SingleAsync();
        report.QuestionVersionId = version.VersionId;
        report.IncidentId = "detail-eligibility-incident";
        database.Context.QuestionReportIncidents.Add(new QuestionReportIncident
        {
            IncidentId = report.IncidentId,
            QuestionId = question.QuestionId,
            QuestionVersionId = version.VersionId,
            Revision = 2,
            Status = "PendingAdminReview",
            RequiresAdminReview = true,
            ProposedResolutionAction = "InvalidateAndAwardFull",
            AdjustmentStatus = "Pending",
            CreatedTime = DateTime.UtcNow
        });
        await database.Context.SaveChangesAsync();

        var handler = new GetQuestionDetailQueryHandler(database.Context);
        var alreadyReported = await handler.Handle(
            new GetQuestionDetailQuery(question.QuestionId, "expert-2", "Expert"),
            CancellationToken.None);
        var eligibleAdmin = await handler.Handle(
            new GetQuestionDetailQuery(question.QuestionId, "admin-1", "Admin"),
            CancellationToken.None);
        var selfReporter = await handler.Handle(
            new GetQuestionDetailQuery(question.QuestionId, "expert-1", "Expert"),
            CancellationToken.None);

        Assert.False(alreadyReported.Value!.ReportEligibility.CanReport);
        Assert.Equal("ALREADY_REPORTED_VERSION", alreadyReported.Value.ReportEligibility.ReasonCode);
        Assert.Equal(version.VersionId, alreadyReported.Value.ReportEligibility.QuestionVersionId);
        Assert.True(alreadyReported.Value.ReportEligibility.RequiresAdminReview);
        Assert.Equal("InvalidateAndAwardFull", alreadyReported.Value.ReportEligibility.ResolutionAction);
        Assert.Equal("Pending", alreadyReported.Value.ReportEligibility.AdjustmentStatus);
        Assert.True(eligibleAdmin.Value!.ReportEligibility.CanReport);
        Assert.False(selfReporter.Value!.ReportEligibility.CanReport);
        Assert.Equal("SELF_REPORT_FORBIDDEN", selfReporter.Value.ReportEligibility.ReasonCode);
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
            DefaultWeight = 1m,
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
