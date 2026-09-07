using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using ClosedXML.Excel;
using MathInsight.Modules.QuestionBank.Contracts.Imports;
using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Modules.QuestionBank.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace MathInsight.Modules.QuestionBank.Tests.System;

/// <summary>
/// L3 boundary tests: real ASP.NET Core routing, authorization, serialization and SQL Server persistence.
/// They are intentionally skipped unless the disposable Docker SQL Server sets QUESTIONBANK_SQLSERVER_CONNECTION.
/// </summary>
public sealed class QuestionBankApiSystemTests : IClassFixture<QuestionBankApiFactory>
{
    private readonly HttpClient _client;
    private readonly QuestionBankApiFactory _factory;

    public QuestionBankApiSystemTests(QuestionBankApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [QuestionBankSqlServerFact]
    public async Task QuestionsEndpoint_WithoutExpertIdentity_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/question-bank/questions?pageIndex=1&pageSize=10");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [QuestionBankSqlServerFact]
    public async Task Expert_Creates_Revises_ThenDeactivatesOwnQuestion_ThroughHostedApi()
    {
        await _factory.SeedAsync(db =>
        {
            db.TagDifficulties.Add(new TagDifficulty
            {
                DifficultyId = "l3-difficulty",
                DifficultyName = "L3 Difficulty",
                LevelValue = 1,
                DisplayOrder = 1,
                IsActive = true
            });
            db.TagTopics.AddRange(
                new TagTopic
                {
                    TagId = "l3-topic-root",
                    TagName = "L3 Topic Root",
                    Grade = 10,
                    DisplayOrder = 1,
                    IsActive = true
                },
                new TagTopic
                {
                    TagId = "l3-topic",
                    ParentTagId = "l3-topic-root",
                    TagName = "L3 Topic",
                    Grade = 10,
                    DisplayOrder = 2,
                    IsActive = true
                });
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/question-bank/questions")
        {
            Content = new StringContent(
                """
                {
                  "questionContent": "What is 2 + 2?",
                  "solutionContent": "4",
                  "difficultyId": "l3-difficulty",
                  "grade": 10,
                  "questionType": "SINGLE_CHOICE",
                  "defaultWeight": 1.0,
                  "topics": [{ "tagId": "l3-topic", "isPrimary": true }],
                  "answers": [
                    { "answerContent": "4", "isCorrect": true },
                    { "answerContent": "5", "isCorrect": false }
                  ],
                  "parts": []
                }
                """,
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add(QuestionBankTestAuthHandler.AccountHeader, "expert_l3");

        var response = await _client.SendAsync(request);

        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected Created but received {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var questionId = await _factory.AssertQuestionWasPersistedAsync("What is 2 + 2?", "expert_l3");

        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/question-bank/questions/{questionId}")
        {
            Content = new StringContent(
                """
                {
                  "questionContent": "What is 3 + 3?",
                  "solutionContent": "6",
                  "difficultyId": "l3-difficulty",
                  "grade": 10,
                  "questionType": "SINGLE_CHOICE",
                  "defaultWeight": 1.0,
                  "topics": [{ "tagId": "l3-topic", "isPrimary": true }],
                  "answers": [
                    { "answerContent": "6", "isCorrect": true },
                    { "answerContent": "7", "isCorrect": false }
                  ],
                  "parts": []
                }
                """,
                Encoding.UTF8,
                "application/json")
        };
        updateRequest.Headers.Add(QuestionBankTestAuthHandler.AccountHeader, "expert_l3");

        var updateResponse = await _client.SendAsync(updateRequest);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        await _factory.AssertQuestionWasUpdatedAsync(questionId);

        using var toggleRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/question-bank/questions/{questionId}/active")
        {
            Content = new StringContent("{ \"isActive\": false }", Encoding.UTF8, "application/json")
        };
        toggleRequest.Headers.Add(QuestionBankTestAuthHandler.AccountHeader, "expert_l3");

        var toggleResponse = await _client.SendAsync(toggleRequest);

        Assert.Equal(HttpStatusCode.OK, toggleResponse.StatusCode);
        await _factory.AssertQuestionWasDeactivatedAsync(questionId);
    }

    [QuestionBankSqlServerFact]
    public async Task ReportWorkflow_WithDifferentExpert_PersistsAndResolvesReportThroughHostedApi()
    {
        var questionId = await _factory.SeedReportableQuestionAsync();

        using var reportRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/question-bank/questions/{questionId}/reports")
        {
            Content = new StringContent("{ \"reportReason\": \"The wording is ambiguous.\" }", Encoding.UTF8, "application/json")
        };
        reportRequest.Headers.Add(QuestionBankTestAuthHandler.AccountHeader, "expert_reporter_l3");

        var reportResponse = await _client.SendAsync(reportRequest);

        Assert.Equal(HttpStatusCode.Created, reportResponse.StatusCode);
        using var reportJson = JsonDocument.Parse(await reportResponse.Content.ReadAsStringAsync());
        var reportId = reportJson.RootElement.GetProperty("reportId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(reportId));

        using var handleRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/question-bank/reports/{reportId}")
        {
            Content = new StringContent("{ \"status\": \"RESOLVED\", \"resolutionAction\": \"NoScoreChange\" }", Encoding.UTF8, "application/json")
        };
        handleRequest.Headers.Add(QuestionBankTestAuthHandler.AccountHeader, "expert_l3");

        var handleResponse = await _client.SendAsync(handleRequest);

        Assert.Equal(HttpStatusCode.OK, handleResponse.StatusCode);
        await _factory.AssertReportWasResolvedAsync(reportId!, questionId);
    }

    [QuestionBankSqlServerFact]
    public async Task DeleteDifficulty_ReferencedByQuestion_SoftDeletesTagAndPreservesQuestionHistory()
    {
        var questionId = await _factory.SeedQuestionWithReferencedDifficultyAsync();

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/question-bank/tags/difficulties/l3-referenced-difficulty");
        request.Headers.Add(QuestionBankTestAuthHandler.AccountHeader, "expert_l3");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await _factory.AssertReferencedDifficultyWasSoftDeletedAsync(questionId);
    }

    [QuestionBankSqlServerFact]
    public async Task DeleteTopic_WithActiveDescendant_ReturnsConflictAndDoesNotMutateTopic()
    {
        await _factory.SeedTopicWithActiveDescendantAsync();

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/question-bank/tags/topics/l3-parent-topic");
        request.Headers.Add(QuestionBankTestAuthHandler.AccountHeader, "expert_l3");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await _factory.AssertTopicRemainsActiveAsync("l3-parent-topic");
    }

    [QuestionBankSqlServerFact]
    public async Task AdminReport_CanBeSubmittedByOwnerAndApprovedByReportingAdmin()
    {
        var questionId = await _factory.SeedReportableQuestionAsync();

        using var reportRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/question-bank/questions/{questionId}/reports")
        {
            Content = new StringContent("{ \"reportReason\": \"Please review the official wording.\" }", Encoding.UTF8, "application/json")
        };
        reportRequest.Headers.Add(QuestionBankTestAuthHandler.AccountHeader, "admin_l3");
        reportRequest.Headers.Add(QuestionBankTestAuthHandler.RoleHeader, "Admin");
        var reportResponse = await _client.SendAsync(reportRequest);
        Assert.Equal(HttpStatusCode.Created, reportResponse.StatusCode);
        using var reportJson = JsonDocument.Parse(await reportResponse.Content.ReadAsStringAsync());
        var reportId = reportJson.RootElement.GetProperty("reportId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(reportId));

        using var submitRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/question-bank/reports/{reportId}/submit-review");
        submitRequest.Headers.Add(QuestionBankTestAuthHandler.AccountHeader, "expert_l3");
        var submitResponse = await _client.SendAsync(submitRequest);
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        using var approveRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/question-bank/admin/reports/{reportId}/approve");
        approveRequest.Headers.Add(QuestionBankTestAuthHandler.AccountHeader, "admin_l3");
        approveRequest.Headers.Add(QuestionBankTestAuthHandler.RoleHeader, "Admin");
        var approveResponse = await _client.SendAsync(approveRequest);

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        await _factory.AssertAdminReportWasApprovedAsync(reportId!, questionId);
    }

    [QuestionBankSqlServerFact]
    public async Task Expert_SubmitsAdminIncidentCorrection_ThroughIncidentRoute()
    {
        var (questionId, versionId) = await _factory.SeedIncidentReportableQuestionAsync();

        using var reportRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/question-bank/questions/{questionId}/reports")
        {
            Content = new StringContent("{ \"reportReason\": \"The answer needs correction.\" }", Encoding.UTF8, "application/json")
        };
        reportRequest.Headers.Add(QuestionBankTestAuthHandler.AccountHeader, "admin_l3");
        reportRequest.Headers.Add(QuestionBankTestAuthHandler.RoleHeader, "Admin");
        var reportResponse = await _client.SendAsync(reportRequest);
        Assert.Equal(HttpStatusCode.Created, reportResponse.StatusCode);

        var incidentId = await _factory.GetIncidentIdAsync(questionId, versionId);
        var submissionPayload = $$"""
                {
                  "expectedRevision": 0,
                  "expectedQuestionVersionId": "{{versionId}}",
                  "submissionKey": "sql-incident-submit-1",
                  "resolutionAction": "NoScoreChange",
                  "reportDecisions": [{ "reportId": "{{(await _factory.GetOnlyReportIdAsync(questionId))}}", "disposition": "Resolved", "reviewNote": "Corrected." }],
                  "correction": {
                    "questionContent": "Corrected SQL incident question",
                    "solutionContent": "The corrected solution is documented.",
                    "difficultyId": "l3-report-difficulty",
                    "grade": 10,
                    "questionType": "SINGLE_CHOICE",
                    "defaultWeight": 1.0,
                    "topics": [{ "tagId": "l3-incident-topic", "isPrimary": true }],
                    "answers": [{ "answerContent": "Correct", "isCorrect": true }, { "answerContent": "Wrong", "isCorrect": false }],
                    "parts": []
                  }
                }
                """;
        HttpRequestMessage CreateSubmitRequest()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/question-report-incidents/{incidentId}/submit")
            {
                Content = new StringContent(submissionPayload, Encoding.UTF8, "application/json")
            };
            request.Headers.Add(QuestionBankTestAuthHandler.AccountHeader, "expert_l3");
            request.Headers.Add(QuestionBankTestAuthHandler.RoleHeader, "Expert");
            return request;
        }

        using var firstSubmitRequest = CreateSubmitRequest();
        using var secondSubmitRequest = CreateSubmitRequest();
        var submitResponses = await Task.WhenAll(
            _client.SendAsync(firstSubmitRequest),
            _client.SendAsync(secondSubmitRequest));

        Assert.All(submitResponses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        await _factory.AssertIncidentSubmissionPendingAdminReviewAsync(incidentId, questionId);
    }

    [QuestionBankSqlServerFact]
    public async Task Expert_PreviewsThenConfirmsValidWorkbook_ThroughHostedApi()
    {
        await _factory.SeedAsync(db =>
        {
            db.TagTopics.AddRange(
                new TagTopic { TagId = "l3-import-topic-root", TagName = "L3 import topic root", Grade = 10, DisplayOrder = 10, IsActive = true },
                new TagTopic { TagId = "l3-import-topic", ParentTagId = "l3-import-topic-root", TagName = "L3 import topic", Grade = 10, DisplayOrder = 11, IsActive = true });
        });

        using var form = new MultipartFormDataContent();
        using var workbookContent = new ByteArrayContent(CreateValidImportWorkbook());
        workbookContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(workbookContent, "file", "l3-question-import.xlsx");
        using var previewRequest = new HttpRequestMessage(HttpMethod.Post, "/api/question-bank/questions/import-preview") { Content = form };
        previewRequest.Headers.Add(QuestionBankTestAuthHandler.AccountHeader, "expert_l3");

        var previewResponse = await _client.SendAsync(previewRequest);

        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = JsonSerializer.Deserialize<QuestionImportPreviewResponse>(
            await previewResponse.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(preview);
        var previewItem = Assert.Single(preview!.Items);
        Assert.True(previewItem.IsValid, string.Join(" | ", previewItem.Errors));
        Assert.NotNull(previewItem.Draft);

        var confirm = new ConfirmQuestionImportRequest
        {
            ImportId = preview.ImportId,
            Items = [new ConfirmQuestionImportItemRequest { QuestionKey = previewItem.QuestionKey, Draft = previewItem.Draft }]
        };
        using var confirmRequest = new HttpRequestMessage(HttpMethod.Post, "/api/question-bank/questions/import-confirm")
        {
            Content = new StringContent(JsonSerializer.Serialize(confirm), Encoding.UTF8, "application/json")
        };
        confirmRequest.Headers.Add(QuestionBankTestAuthHandler.AccountHeader, "expert_l3");

        var confirmResponse = await _client.SendAsync(confirmRequest);

        Assert.Equal(HttpStatusCode.Created, confirmResponse.StatusCode);
        await _factory.AssertQuestionWasPersistedAsync("Imported L3 question", "expert_l3");
    }

    private static byte[] CreateValidImportWorkbook()
    {
        using var workbook = new XLWorkbook();
        workbook.Worksheets.Add("_Meta").Cell(1, 1).Value = "TemplateVersion";
        workbook.Worksheet("_Meta").Cell(1, 2).Value = "3";
        workbook.Worksheets.Add("Instructions");
        AddSheet(workbook, "Questions", ["QuestionKey", "QuestionContent", "SolutionContent", "QuestionType", "Grade", "DifficultyLevel", "DefaultWeight", "PictureUrl"]);
        var questions = workbook.Worksheet("Questions");
        questions.Cell(2, 1).Value = "L3-IMPORT-001";
        questions.Cell(2, 2).Value = "Imported L3 question";
        questions.Cell(2, 3).Value = "The import was confirmed.";
        questions.Cell(2, 4).Value = "SINGLE_CHOICE";
        questions.Cell(2, 5).Value = 10;
        questions.Cell(2, 6).Value = 2;
        questions.Cell(2, 7).Value = 1;
        AddSheet(workbook, "Answers", ["QuestionKey", "AnswerContent", "IsCorrect"]);
        var answers = workbook.Worksheet("Answers");
        answers.Cell(2, 1).Value = "L3-IMPORT-001"; answers.Cell(2, 2).Value = "Confirmed"; answers.Cell(2, 3).Value = true;
        answers.Cell(3, 1).Value = "L3-IMPORT-001"; answers.Cell(3, 2).Value = "Rejected"; answers.Cell(3, 3).Value = false;
        AddSheet(workbook, "Parts", ["QuestionKey", "PartOrder", "PartLabel", "PartContent", "PartType", "CorrectBoolean", "CorrectText", "CorrectNumeric", "NumericTolerance", "Explanation", "DefaultWeight"]);
        AddSheet(workbook, "Topics", ["QuestionKey", "TopicCode", "IsPrimary"]);
        var topics = workbook.Worksheet("Topics");
        topics.Cell(2, 1).Value = "L3-IMPORT-001"; topics.Cell(2, 2).Value = "l3-import-topic"; topics.Cell(2, 3).Value = true;
        workbook.Worksheets.Add("Catalogs");
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void AddSheet(XLWorkbook workbook, string name, IReadOnlyList<string> headers)
    {
        var sheet = workbook.Worksheets.Add(name);
        for (var index = 0; index < headers.Count; index++) sheet.Cell(1, index + 1).Value = headers[index];
    }
}

public sealed class QuestionBankApiFactory : WebApplicationFactory<Program>
{
    private const string ConnectionEnvironmentVariable = "QUESTIONBANK_SQLSERVER_CONNECTION";
    private readonly string _databaseName = $"MathInsightQuestionBankApiL3_{Guid.NewGuid():N}";
    private readonly string? _masterConnectionString;
    private readonly string? _sqlConnectionString;

    public QuestionBankApiFactory()
    {
        var sourceConnectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(sourceConnectionString))
            return;

        _masterConnectionString = WithDatabase(sourceConnectionString, "master");
        _sqlConnectionString = WithDatabase(sourceConnectionString, _databaseName);
        ExecuteNonQueryAsync(_masterConnectionString, $"CREATE DATABASE [{_databaseName}]").GetAwaiter().GetResult();
        ExecuteSqlScriptAsync(_sqlConnectionString, FindRepositoryFile("database", "001_Create_MathInsight_Azure.sql")).GetAwaiter().GetResult();
        ExecuteSqlScriptAsync(_sqlConnectionString, FindRepositoryFile("database", "005_Align_TestGen_QuestionBank_Contract.sql")).GetAwaiter().GetResult();
        ExecuteSqlScriptAsync(_sqlConnectionString, FindRepositoryFile("database", "006_MentorFollowUp_CompositePolicy.sql")).GetAwaiter().GetResult();
        ExecuteNonQueryAsync(_sqlConnectionString, """
            INSERT INTO dbo.[Role] (RoleID, RoleName, Description) VALUES
                ('role-expert-l3', N'Expert', N'L3 test role'),
                ('role-admin-l3', N'Admin', N'L3 test role');
            INSERT INTO dbo.Account (AccountID, Username, PasswordHash, Email, FirstName, LastName, RoleID, isActive)
            VALUES
                ('expert_l3', N'expert_l3', 'hash', 'expert_l3@example.test', N'Expert', N'L3', 'role-expert-l3', 1),
                ('expert_reporter_l3', N'expert_reporter_l3', 'hash', 'expert_reporter_l3@example.test', N'Reporter', N'L3', 'role-expert-l3', 1),
                ('admin_l3', N'admin_l3', 'hash', 'admin_l3@example.test', N'Admin', N'L3', 'role-admin-l3', 1);
            INSERT INTO dbo.Expert (ExpertID, Specialty) VALUES
                ('expert_l3', N'L3 test owner'),
                ('expert_reporter_l3', N'L3 test reporter');
            INSERT INTO dbo.TagDifficulty (DifficultyID, DifficultyName, Description, LevelValue, DisplayOrder, IsActive)
            VALUES ('l3-report-difficulty', N'L3 Report Difficulty', N'Shared report-test difficulty', 2, 2, 1);
            """).GetAwaiter().GetResult();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _sqlConnectionString,
                ["RabbitMQ:Enabled"] = "false"
            }));
        builder.ConfigureServices(services =>
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = QuestionBankTestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = QuestionBankTestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, QuestionBankTestAuthHandler>(QuestionBankTestAuthHandler.SchemeName, _ => { }));
    }

    public async Task SeedAsync(Action<QuestionBankDbContext> seed)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuestionBankDbContext>();
        seed(db);
        await db.SaveChangesAsync();
    }

    public async Task<string> AssertQuestionWasPersistedAsync(string content, string expertId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuestionBankDbContext>();
        var question = await db.Questions
            .Include(item => item.Answers)
            .Include(item => item.QuestionTopics)
            .SingleAsync(item => item.QuestionContent == content);

        Assert.Equal(expertId, question.ExpertId);
        Assert.True(question.IsActive);
        Assert.Equal("Approved", question.Status);
        Assert.Equal(2, question.Answers.Count);
        Assert.Single(question.QuestionTopics);
        return question.QuestionId;
    }

    public async Task AssertQuestionWasDeactivatedAsync(string questionId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuestionBankDbContext>();
        var question = await db.Questions.SingleAsync(item => item.QuestionId == questionId);

        Assert.False(question.IsActive);
        Assert.Equal("Deactivated", question.Status);
    }

    public async Task AssertQuestionWasUpdatedAsync(string questionId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuestionBankDbContext>();
        var question = await db.Questions
            .Include(item => item.Answers)
            .Include(item => item.Versions)
            .SingleAsync(item => item.QuestionId == questionId);

        Assert.Equal("What is 3 + 3?", question.QuestionContent);
        Assert.Equal("6", question.SolutionContent);
        Assert.Equal(2, question.Versions.Count);

        var activeAnswers = question.Answers.Where(answer => !answer.IsArchived).ToList();
        Assert.Equal(2, activeAnswers.Count);
        Assert.Contains(activeAnswers, answer => answer.AnswerContent == "6" && answer.IsCorrect);
        Assert.Contains(activeAnswers, answer => answer.AnswerContent == "7" && !answer.IsCorrect);
    }

    public async Task<string> SeedReportableQuestionAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..16];
        var questionId = $"l3-rq-{suffix}";
        await SeedAsync(db =>
        {
            db.Questions.Add(new Question
            {
                QuestionId = questionId,
                QuestionContent = "Reportable L3 question",
                SolutionContent = "solution",
                DifficultyId = "l3-report-difficulty",
                Grade = 10,
                Status = "Approved",
                QuestionType = "SingleChoice",
                ExpertId = "expert_l3",
                DefaultWeight = 1m,
                IsActive = true,
                CreatedTime = DateTime.UtcNow,
                UpdatedTime = DateTime.UtcNow
            });
        });
        return questionId;
    }

    public async Task<(string QuestionId, string VersionId)> SeedIncidentReportableQuestionAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..16];
        var questionId = $"l3-incident-{suffix}";
        var versionId = $"l3-version-{suffix}";
        await SeedAsync(db =>
        {
            db.TagTopics.AddRange(
                new TagTopic { TagId = "l3-incident-root", TagName = "L3 incident root", Grade = 10, DisplayOrder = 30, IsActive = true },
                new TagTopic { TagId = "l3-incident-topic", ParentTagId = "l3-incident-root", TagName = "L3 incident topic", Grade = 10, DisplayOrder = 31, IsActive = true });
            db.Questions.Add(new Question
            {
                QuestionId = questionId,
                QuestionContent = "Original SQL incident question",
                SolutionContent = "Original documented solution",
                DifficultyId = "l3-report-difficulty",
                Grade = 10,
                Status = "Approved",
                QuestionType = "SingleChoice",
                ExpertId = "expert_l3",
                DefaultWeight = 1m,
                IsActive = true,
                CreatedTime = DateTime.UtcNow,
                UpdatedTime = DateTime.UtcNow,
                Answers =
                [
                    new Answer { AnswerId = $"l3-answer-a-{suffix}", AnswerContent = "Original", IsCorrect = true },
                    new Answer { AnswerId = $"l3-answer-b-{suffix}", AnswerContent = "Other", IsCorrect = false }
                ],
                QuestionTopics =
                [
                    new QuestionTopic { QuestionTopicId = $"l3-topic-link-{suffix}", TagId = "l3-incident-topic", IsPrimary = true }
                ],
                Versions =
                [
                    new QuestionVersion
                    {
                        VersionId = versionId,
                        QuestionContent = "Original SQL incident question",
                        QuestionAnswer = "Original documented solution",
                        AnswersSnapshot = "{}",
                        VersionNumber = 1,
                        SnapshotSchemaVersion = 2,
                        CreatedTime = DateTime.UtcNow,
                        ExpertId = "expert_l3"
                    }
                ]
            });
        });
        return (questionId, versionId);
    }

    public async Task<string> GetIncidentIdAsync(string questionId, string versionId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuestionBankDbContext>();
        return await db.QuestionReportIncidents
            .Where(item => item.QuestionId == questionId && item.QuestionVersionId == versionId)
            .Select(item => item.IncidentId)
            .SingleAsync();
    }

    public async Task<string> GetOnlyReportIdAsync(string questionId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuestionBankDbContext>();
        return await db.QuestionReports
            .Where(item => item.QuestionId == questionId)
            .Select(item => item.ReportId)
            .SingleAsync();
    }

    public async Task AssertIncidentSubmissionPendingAdminReviewAsync(string incidentId, string questionId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuestionBankDbContext>();
        var incident = await db.QuestionReportIncidents.SingleAsync(item => item.IncidentId == incidentId);
        var report = await db.QuestionReports.SingleAsync(item => item.IncidentId == incidentId);
        Assert.Equal("PendingAdminReview", incident.Status);
        Assert.NotNull(incident.SubmittedCorrectionVersionId);
        Assert.Equal("PendingReview", report.Status);
        Assert.Equal(2, await db.QuestionVersions.CountAsync(item => item.QuestionId == questionId));
    }

    public async Task AssertReportWasResolvedAsync(string reportId, string questionId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuestionBankDbContext>();
        var report = await db.QuestionReports.SingleAsync(item => item.ReportId == reportId);
        var question = await db.Questions.SingleAsync(item => item.QuestionId == questionId);

        Assert.Equal("Resolved", report.Status);
        Assert.Equal("Approved", question.Status);
    }

    public async Task<string> SeedQuestionWithReferencedDifficultyAsync()
    {
        const string questionId = "l3-referenced-difficulty-question";
        await SeedAsync(db =>
        {
            db.TagDifficulties.Add(new TagDifficulty { DifficultyId = "l3-referenced-difficulty", DifficultyName = "Referenced difficulty", LevelValue = 3, DisplayOrder = 3, IsActive = true });
            db.TagTopics.Add(new TagTopic { TagId = "l3-referenced-topic", TagName = "Referenced topic", Grade = 10, DisplayOrder = 3, IsActive = true });
            db.Questions.Add(new Question
            {
                QuestionId = questionId,
                QuestionContent = "Question retaining its soft-deleted difficulty",
                SolutionContent = "solution",
                DifficultyId = "l3-referenced-difficulty",
                Grade = 10,
                Status = "Approved",
                QuestionType = "SingleChoice",
                ExpertId = "expert_l3",
                DefaultWeight = 1m,
                IsActive = true,
                CreatedTime = DateTime.UtcNow,
                UpdatedTime = DateTime.UtcNow
            });
        });
        return questionId;
    }

    public async Task AssertReferencedDifficultyWasSoftDeletedAsync(string questionId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuestionBankDbContext>();
        var difficulty = await db.TagDifficulties.SingleAsync(item => item.DifficultyId == "l3-referenced-difficulty");
        var question = await db.Questions.SingleAsync(item => item.QuestionId == questionId);
        Assert.False(difficulty.IsActive);
        Assert.Equal("l3-referenced-difficulty", question.DifficultyId);
    }

    public async Task SeedTopicWithActiveDescendantAsync()
    {
        await SeedAsync(db =>
        {
            db.TagTopics.AddRange(
                new TagTopic { TagId = "l3-parent-topic", TagName = "L3 parent", Grade = 10, DisplayOrder = 4, IsActive = true },
                new TagTopic { TagId = "l3-child-topic", ParentTagId = "l3-parent-topic", TagName = "L3 child", Grade = 10, DisplayOrder = 5, IsActive = true });
        });
    }

    public async Task AssertTopicRemainsActiveAsync(string tagId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuestionBankDbContext>();
        Assert.True((await db.TagTopics.SingleAsync(item => item.TagId == tagId)).IsActive);
    }

    public async Task AssertAdminReportWasApprovedAsync(string reportId, string questionId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuestionBankDbContext>();
        var report = await db.QuestionReports.SingleAsync(item => item.ReportId == reportId);
        var question = await db.Questions.SingleAsync(item => item.QuestionId == questionId);
        Assert.Equal("Resolved", report.Status);
        Assert.Equal("admin_l3", report.ReviewedBy);
        Assert.Equal("Approved", question.Status);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!string.IsNullOrWhiteSpace(_masterConnectionString))
            DropDatabaseIfExistsAsync(_masterConnectionString, _databaseName).GetAwaiter().GetResult();
    }

    private static string WithDatabase(string connectionString, string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = databaseName };
        return builder.ConnectionString;
    }

    private static string FindRepositoryFile(params string[] pathParts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. pathParts]);
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException($"Repository file was not found: {Path.Combine(pathParts)}");
    }

    private static async Task ExecuteSqlScriptAsync(string connectionString, string scriptPath)
    {
        var script = await File.ReadAllTextAsync(scriptPath);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        foreach (var batch in global::System.Text.RegularExpressions.Regex.Split(script, @"(?im)^\s*GO\s*(?:--.*)?$"))
        {
            if (!string.IsNullOrWhiteSpace(batch))
            {
                await using var command = new SqlCommand(batch, connection);
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    private static async Task ExecuteNonQueryAsync(string connectionString, string commandText)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(commandText, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static Task DropDatabaseIfExistsAsync(string masterConnectionString, string databaseName)
        => ExecuteNonQueryAsync(masterConnectionString, $"""
            IF DB_ID(N'{databaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{databaseName}];
            END;
            """);
}

public sealed class QuestionBankTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "QuestionBankL3Test";
    public const string AccountHeader = "X-Test-Account-Id";
    public const string RoleHeader = "X-Test-Role";

    public QuestionBankTestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var accountId = Request.Headers[AccountHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(accountId))
            return Task.FromResult(AuthenticateResult.NoResult());

        var role = Request.Headers[RoleHeader].FirstOrDefault() ?? "Expert";

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, accountId), new Claim(ClaimTypes.Role, role)],
            SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

public sealed class QuestionBankSqlServerFactAttribute : FactAttribute
{
    public QuestionBankSqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("QUESTIONBANK_SQLSERVER_CONNECTION")))
            Skip = "Set QUESTIONBANK_SQLSERVER_CONNECTION to run the disposable SQL Server system tests.";
    }
}
