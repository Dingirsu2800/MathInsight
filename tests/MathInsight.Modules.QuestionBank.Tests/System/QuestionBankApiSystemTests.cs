using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
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
    public async Task CreateQuestion_WithExpertIdentity_PersistsQuestionThroughHostedApi()
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
            db.TagTopics.Add(new TagTopic
            {
                TagId = "l3-topic",
                TagName = "L3 Topic",
                Grade = 10,
                DisplayOrder = 1,
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

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
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
        ApplyAzureQuestionReportSchemaAsync(_sqlConnectionString).GetAwaiter().GetResult();
        ExecuteNonQueryAsync(_sqlConnectionString, """
            INSERT INTO dbo.[Role] (RoleID, RoleName, Description) VALUES ('role-expert-l3', N'Expert', N'L3 test role');
            INSERT INTO dbo.Account (AccountID, Username, PasswordHash, Email, FirstName, LastName, RoleID, isActive)
            VALUES
                ('expert_l3', N'expert_l3', 'hash', 'expert_l3@example.test', N'Expert', N'L3', 'role-expert-l3', 1),
                ('expert_reporter_l3', N'expert_reporter_l3', 'hash', 'expert_reporter_l3@example.test', N'Reporter', N'L3', 'role-expert-l3', 1);
            INSERT INTO dbo.Expert (ExpertID, Specialty) VALUES
                ('expert_l3', N'L3 test owner'),
                ('expert_reporter_l3', N'L3 test reporter');
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
        const string questionId = "l3-report-question";
        await SeedAsync(db =>
        {
            db.TagDifficulties.Add(new TagDifficulty { DifficultyId = "l3-report-difficulty", DifficultyName = "L3 Report Difficulty", LevelValue = 2, DisplayOrder = 2, IsActive = true });
            db.TagTopics.Add(new TagTopic { TagId = "l3-report-topic", TagName = "L3 Report Topic", Grade = 10, DisplayOrder = 2, IsActive = true });
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

    public async Task AssertReportWasResolvedAsync(string reportId, string questionId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuestionBankDbContext>();
        var report = await db.QuestionReports.SingleAsync(item => item.ReportId == reportId);
        var question = await db.Questions.SingleAsync(item => item.QuestionId == questionId);

        Assert.Equal("Resolved", report.Status);
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
        foreach (var batch in global::System.Text.RegularExpressions.Regex.Split(script, @"(?im)^\s*GO\s*(?:--.*)?$"))
        {
            if (!string.IsNullOrWhiteSpace(batch))
                await ExecuteNonQueryAsync(connectionString, batch);
        }
    }

    // Test-only, disposable-schema alignment. Azure already contains these columns; this does not
    // run against Azure or modify repository database scripts.
    private static Task ApplyAzureQuestionReportSchemaAsync(string connectionString) => ExecuteNonQueryAsync(connectionString, """
        IF COL_LENGTH(N'dbo.QuestionReport', N'SessionID') IS NULL
            ALTER TABLE dbo.QuestionReport ADD SessionID VARCHAR(36) NULL;
        IF COL_LENGTH(N'dbo.QuestionReport', N'QuestionVersionID') IS NULL
            ALTER TABLE dbo.QuestionReport ADD QuestionVersionID VARCHAR(36) NULL;
        IF COL_LENGTH(N'dbo.QuestionReport', N'ResolutionAction') IS NULL
            ALTER TABLE dbo.QuestionReport ADD ResolutionAction VARCHAR(30) NULL;
        IF COL_LENGTH(N'dbo.QuestionReport', N'ScoreAdjustedTime') IS NULL
            ALTER TABLE dbo.QuestionReport ADD ScoreAdjustedTime DATETIME2(0) NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_QuestionReport_QuestionVersion_QuestionVersionID')
            EXEC(N'ALTER TABLE dbo.QuestionReport ADD CONSTRAINT FK_QuestionReport_QuestionVersion_QuestionVersionID
                FOREIGN KEY (QuestionVersionID) REFERENCES dbo.QuestionVersion(VersionID)');
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_QuestionReport_Version_ResolutionAction' AND object_id = OBJECT_ID(N'dbo.QuestionReport'))
            EXEC(N'CREATE INDEX IX_QuestionReport_Version_ResolutionAction ON dbo.QuestionReport (QuestionVersionID, ResolutionAction)
                WHERE QuestionVersionID IS NOT NULL');
        """);

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

    public QuestionBankTestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var accountId = Request.Headers[AccountHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(accountId))
            return Task.FromResult(AuthenticateResult.NoResult());

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, accountId), new Claim(ClaimTypes.Role, "Expert")],
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
