using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Modules.TestGen.Persistence.ReadModels;
using MathInsight.Shared.Questions;
using MathInsight.Modules.TestGen.Persistence;
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

namespace MathInsight.Modules.TestGen.Tests.System;

/// <summary>
/// L3 boundary tests: ASP.NET Core routing, Expert authorization, JSON contracts and SQL Server persistence.
/// They require TESTGEN_SQLSERVER_CONNECTION, normally supplied by scripts/run-l3-sql-smoke.ps1.
/// </summary>
public sealed class BlueprintApiSystemTests : IClassFixture<BlueprintApiFactory>
{
    private readonly BlueprintApiFactory _factory;
    private readonly HttpClient _client;

    public BlueprintApiSystemTests(BlueprintApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [TestGenSqlServerFact]
    public async Task BlueprintsEndpoint_WithoutExpertIdentity_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/test-generator/blueprints?pageIndex=1&pageSize=10");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestGenSqlServerFact]
    public async Task CreateBlueprint_AsOwner_PersistsDraftSectionsAndDetailsThroughHostedApi()
    {
        using var request = CreateJsonRequest(HttpMethod.Post, "/api/test-generator/blueprints", ValidBlueprintRequestJson("L3 persisted blueprint"));
        request.Headers.Add(BlueprintTestAuthHandler.AccountHeader, "expert_l3_owner");

        var createResponse = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var blueprintId = await ReadBlueprintIdAsync(createResponse);
        await _factory.AssertBlueprintWasPersistedAsync(blueprintId, "expert_l3_owner", "L3 persisted blueprint");

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/test-generator/blueprints/{blueprintId}");
        getRequest.Headers.Add(BlueprintTestAuthHandler.AccountHeader, "expert_l3_owner");
        var getResponse = await _client.SendAsync(getRequest);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [TestGenSqlServerFact]
    public async Task UpdateBlueprint_AsDifferentExpert_ReturnsForbiddenAndPreservesOwnerBlueprint()
    {
        var blueprintId = await CreateBlueprintAsOwnerAsync("L3 protected blueprint");
        using var request = CreateJsonRequest(HttpMethod.Put, $"/api/test-generator/blueprints/{blueprintId}", ValidBlueprintRequestJson("Tampered blueprint"));
        request.Headers.Add(BlueprintTestAuthHandler.AccountHeader, "expert_l3_other");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertErrorCodeAsync(response, "BLUEPRINT_MUTATION_FORBIDDEN");
        await _factory.AssertBlueprintRemainsOwnedAndUnchangedAsync(blueprintId, "expert_l3_owner", "L3 protected blueprint");
    }

    [TestGenSqlServerFact]
    public async Task CreateBlueprint_WithTaxonomyFromDifferentGrade_ReturnsBadRequestAndDoesNotPersist()
    {
        var initialCount = await _factory.CountBlueprintsAsync();
        using var request = CreateJsonRequest(HttpMethod.Post, "/api/test-generator/blueprints", ValidBlueprintRequestJson("Invalid taxonomy blueprint", tagId: "l3-topic-11"));
        request.Headers.Add(BlueprintTestAuthHandler.AccountHeader, "expert_l3_owner");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertErrorCodeAsync(response, "BLUEPRINT_TAXONOMY_INVALID");
        Assert.Equal(initialCount, await _factory.CountBlueprintsAsync());
    }

    [TestGenSqlServerFact]
    public async Task Owner_CreatesAndReadsBlueprint_ThenOtherExpertCannotModifyIt_ThroughHostedApi()
    {
        var originalName = $"L3 ownership journey {Guid.NewGuid():N}";
        using var createRequest = CreateJsonRequest(HttpMethod.Post, "/api/test-generator/blueprints", ValidBlueprintRequestJson(originalName));
        createRequest.Headers.Add(BlueprintTestAuthHandler.AccountHeader, "expert_l3_owner");

        var createResponse = await _client.SendAsync(createRequest);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var blueprintId = await ReadBlueprintIdAsync(createResponse);
        await _factory.AssertBlueprintWasPersistedAsync(blueprintId, "expert_l3_owner", originalName);

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/test-generator/blueprints/{blueprintId}");
        getRequest.Headers.Add(BlueprintTestAuthHandler.AccountHeader, "expert_l3_owner");
        var getResponse = await _client.SendAsync(getRequest);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Contains(originalName, await getResponse.Content.ReadAsStringAsync());

        using var updateRequest = CreateJsonRequest(HttpMethod.Put, $"/api/test-generator/blueprints/{blueprintId}", ValidBlueprintRequestJson("Unauthorized replacement"));
        updateRequest.Headers.Add(BlueprintTestAuthHandler.AccountHeader, "expert_l3_other");
        var updateResponse = await _client.SendAsync(updateRequest);

        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);
        await AssertErrorCodeAsync(updateResponse, "BLUEPRINT_MUTATION_FORBIDDEN");
        await _factory.AssertBlueprintRemainsOwnedAndUnchangedAsync(blueprintId, "expert_l3_owner", originalName);
    }

    [TestGenSqlServerFact]
    public async Task Student_ViewsTopicOptionsThenGeneratesTenQuestionPractice_ThroughHostedApi()
    {
        var scenario = await _factory.SeedTopicPracticeScenarioAsync();
        using var optionsRequest = new HttpRequestMessage(HttpMethod.Get, "/api/test-generator/tests/topic-practice-options");
        optionsRequest.Headers.Add(BlueprintTestAuthHandler.AccountHeader, scenario.StudentId);
        optionsRequest.Headers.Add(BlueprintTestAuthHandler.RoleHeader, "Student");

        var optionsResponse = await _client.SendAsync(optionsRequest);

        Assert.Equal(HttpStatusCode.OK, optionsResponse.StatusCode);
        Assert.Contains(scenario.TagId, await optionsResponse.Content.ReadAsStringAsync());

        using var generateRequest = CreateJsonRequest(HttpMethod.Post, "/api/test-generator/tests/topic-practices", $$"""{ "tagId": "{{scenario.TagId}}" }""");
        generateRequest.Headers.Add(BlueprintTestAuthHandler.AccountHeader, scenario.StudentId);
        generateRequest.Headers.Add(BlueprintTestAuthHandler.RoleHeader, "Student");

        var generateResponse = await _client.SendAsync(generateRequest);

        Assert.Equal(HttpStatusCode.Created, generateResponse.StatusCode);
        using var responseJson = JsonDocument.Parse(await generateResponse.Content.ReadAsStringAsync());
        var testId = responseJson.RootElement.GetProperty("testId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(testId));
        Assert.Equal(10, responseJson.RootElement.GetProperty("totalQuestions").GetInt32());
        Assert.Equal("TopicPractice", responseJson.RootElement.GetProperty("testMode").GetString());
        await _factory.AssertTopicPracticeWasPersistedAsync(testId!, scenario.StudentId, scenario.TagId);
    }

    [TestGenSqlServerFact]
    public async Task Student_GeneratesTopicPracticeWithInsufficientPool_ReturnsConflictAndWritesNothing()
    {
        var scenario = await _factory.SeedInsufficientTopicPracticeScenarioAsync();
        var before = await _factory.CountTopicPracticeTestsAsync(scenario.StudentId);
        using var request = CreateJsonRequest(HttpMethod.Post, "/api/test-generator/tests/topic-practices", $$"""{ "tagId": "{{scenario.TagId}}" }""");
        request.Headers.Add(BlueprintTestAuthHandler.AccountHeader, scenario.StudentId);
        request.Headers.Add(BlueprintTestAuthHandler.RoleHeader, "Student");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertErrorCodeAsync(response, "TOPIC_PRACTICE_INSUFFICIENT_QUESTIONS");
        Assert.Equal(before, await _factory.CountTopicPracticeTestsAsync(scenario.StudentId));
    }

    private async Task<string> CreateBlueprintAsOwnerAsync(string name)
    {
        using var request = CreateJsonRequest(HttpMethod.Post, "/api/test-generator/blueprints", ValidBlueprintRequestJson(name));
        request.Headers.Add(BlueprintTestAuthHandler.AccountHeader, "expert_l3_owner");
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadBlueprintIdAsync(response);
    }

    private static HttpRequestMessage CreateJsonRequest(HttpMethod method, string path, string body) => new(method, path)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private static string ValidBlueprintRequestJson(string name, string tagId = "l3-topic-12") => $$"""
        {
          "blueprintName": "{{name}}",
          "grade": 12,
          "totalQuestions": 1,
          "totalScore": 10.00,
          "durationMinutes": 15,
          "sections": [{
            "sectionOrder": 1,
            "sectionName": "Section I",
            "questionType": "SingleChoice",
            "totalQuestions": 1,
            "scoreBudget": 10.00,
            "scoringRule": "AllOrNothing",
            "details": [{
              "tagId": "{{tagId}}",
              "difficultyId": "l3-difficulty-1",
              "quantity": 1
            }]
          }]
        }
        """;

    private static async Task<string> ReadBlueprintIdAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var blueprintId = document.RootElement.GetProperty("blueprintId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(blueprintId));
        return blueprintId!;
    }

    private static async Task AssertErrorCodeAsync(HttpResponseMessage response, string expectedCode)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
    }
}

public sealed class BlueprintApiFactory : WebApplicationFactory<Program>
{
    private const string ConnectionEnvironmentVariable = "TESTGEN_SQLSERVER_CONNECTION";
    private readonly string _databaseName = $"MathInsightBlueprintApiL3_{Guid.NewGuid():N}";
    private readonly string? _masterConnectionString;
    private readonly string? _sqlConnectionString;

    public BlueprintApiFactory()
    {
        var sourceConnectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(sourceConnectionString))
            return;

        _masterConnectionString = WithDatabase(sourceConnectionString, "master");
        _sqlConnectionString = WithDatabase(sourceConnectionString, _databaseName);
        ExecuteNonQueryAsync(_masterConnectionString, $"CREATE DATABASE [{_databaseName}]").GetAwaiter().GetResult();
        ExecuteSqlScriptAsync(_sqlConnectionString, FindRepositoryFile("database", "001_Create_MathInsight_Azure.sql")).GetAwaiter().GetResult();
        ExecuteSqlScriptAsync(_sqlConnectionString, FindRepositoryFile("database", "005_Align_TestGen_QuestionBank_Contract.sql")).GetAwaiter().GetResult();
        ExecuteNonQueryAsync(_sqlConnectionString, """
            INSERT INTO dbo.[Role] (RoleID, RoleName, Description) VALUES ('role-expert-l3', N'Expert', N'L3 test role');
            INSERT INTO dbo.[Role] (RoleID, RoleName, Description) VALUES ('role-student-l3', N'Student', N'L3 test role');
            INSERT INTO dbo.Account (AccountID, Username, PasswordHash, Email, FirstName, LastName, RoleID, isActive) VALUES
                ('expert_l3_owner', N'expert_l3_owner', 'hash', 'owner@example.test', N'Owner', N'L3', 'role-expert-l3', 1),
                ('expert_l3_other', N'expert_l3_other', 'hash', 'other@example.test', N'Other', N'L3', 'role-expert-l3', 1),
                ('student_l3_topic', N'student_l3_topic', 'hash', 'student@example.test', N'Student', N'L3', 'role-student-l3', 1);
            INSERT INTO dbo.Expert (ExpertID, Specialty) VALUES
                ('expert_l3_owner', N'Mathematics'), ('expert_l3_other', N'Mathematics');
            INSERT INTO dbo.Student (StudentID, CurrentGrade) VALUES ('student_l3_topic', 12);
            INSERT INTO dbo.TagTopic (TagID, TagName, Grade, DisplayOrder, IsActive) VALUES
                ('l3-topic-12', N'L3 grade 12 topic', 12, 1, 1),
                ('l3-topic-11', N'L3 grade 11 topic', 11, 2, 1);
            INSERT INTO dbo.TagDifficulty (DifficultyID, DifficultyName, LevelValue, DisplayOrder, IsActive) VALUES
                ('l3-difficulty-1', N'L3 Foundation', 1, 1, 1);
            """).GetAwaiter().GetResult();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = _sqlConnectionString,
            ["RabbitMQ:Enabled"] = "false"
        }));
        builder.ConfigureServices(services =>
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = BlueprintTestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = BlueprintTestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, BlueprintTestAuthHandler>(BlueprintTestAuthHandler.SchemeName, _ => { }));
    }

    public async Task AssertBlueprintWasPersistedAsync(string blueprintId, string expertId, string blueprintName)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TestGenDbContext>();
        var blueprint = await db.Blueprints.Include(item => item.Sections).ThenInclude(item => item.Details)
            .SingleAsync(item => item.BlueprintId == blueprintId);
        Assert.Equal(expertId, blueprint.ExpertId);
        Assert.Equal(blueprintName, blueprint.BlueprintName);
        Assert.Equal("Draft", blueprint.Status);
        var section = Assert.Single(blueprint.Sections);
        Assert.Equal("SingleChoice", section.QuestionType);
        var detail = Assert.Single(section.Details);
        Assert.Equal("l3-topic-12", detail.TagId);
        Assert.Equal("l3-difficulty-1", detail.DifficultyId);
    }

    public async Task AssertBlueprintRemainsOwnedAndUnchangedAsync(string blueprintId, string expectedOwner, string expectedName)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TestGenDbContext>();
        var blueprint = await db.Blueprints.SingleAsync(item => item.BlueprintId == blueprintId);
        Assert.Equal(expectedOwner, blueprint.ExpertId);
        Assert.Equal(expectedName, blueprint.BlueprintName);
    }

    public async Task<int> CountBlueprintsAsync()
    {
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TestGenDbContext>().Blueprints.CountAsync();
    }

    public async Task<(string StudentId, string TagId)> SeedTopicPracticeScenarioAsync()
    {
        const string studentId = "student_l3_topic";
        const string tagId = "l3-topic-practice";
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TestGenDbContext>();
        db.TagTopics.Add(new TagTopicReadModel { TagId = tagId, TagName = "L3 topic practice", Grade = 12, DisplayOrder = 10, IsActive = true });
        await db.SaveChangesAsync();
        for (var index = 1; index <= 10; index++)
        {
            var questionId = $"l3tp-q-{index:00}";
            var answerId = $"l3tp-a-{index:00}";
            var snapshot = new QuestionSnapshotV2(questionId, "SingleChoice", "l3-difficulty-1", 12, 1m,
                [new QuestionTopicSnapshot(tagId, true)], [new QuestionAnswerSnapshot(answerId, "Correct", true)], [], $"L3 topic practice question {index}", "Solution");
            await SeedTopicPracticeQuestionAsync(_sqlConnectionString!, questionId, answerId, $"l3tp-v-{index:00}", tagId, JsonSerializer.Serialize(snapshot));
        }
        return (studentId, tagId);
    }

    public async Task<(string StudentId, string TagId)> SeedInsufficientTopicPracticeScenarioAsync()
    {
        const string studentId = "student_l3_topic";
        const string tagId = "l3-topic-practice-insufficient";
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TestGenDbContext>();
        db.TagTopics.Add(new TagTopicReadModel { TagId = tagId, TagName = "L3 insufficient topic", Grade = 12, DisplayOrder = 11, IsActive = true });
        await db.SaveChangesAsync();
        return (studentId, tagId);
    }

    public async Task AssertTopicPracticeWasPersistedAsync(string testId, string studentId, string tagId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TestGenDbContext>();
        var test = await db.Tests.Include(item => item.Questions).SingleAsync(item => item.TestId == testId);
        Assert.Equal(studentId, test.GeneratedForStudentId);
        Assert.Equal("TopicPractice", test.TestMode);
        Assert.Equal(10, test.TotalQuestions);
        Assert.Equal(10, test.Questions.Count);
        Assert.All(test.Questions, question => Assert.Equal(tagId, question.RecommendedForTagId));
    }

    public async Task<int> CountTopicPracticeTestsAsync(string studentId)
    {
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TestGenDbContext>().Tests
            .CountAsync(test => test.GeneratedForStudentId == studentId && test.TestMode == "TopicPractice");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!string.IsNullOrWhiteSpace(_masterConnectionString))
            DropDatabaseIfExistsAsync(_masterConnectionString, _databaseName).GetAwaiter().GetResult();
    }

    private static string WithDatabase(string connectionString, string databaseName)
        => new SqlConnectionStringBuilder(connectionString) { InitialCatalog = databaseName }.ConnectionString;

    private static string FindRepositoryFile(params string[] pathParts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. pathParts]);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException($"Repository file was not found: {Path.Combine(pathParts)}");
    }

    private static async Task ExecuteSqlScriptAsync(string connectionString, string scriptPath)
    {
        var script = await File.ReadAllTextAsync(scriptPath);
        foreach (var batch in global::System.Text.RegularExpressions.Regex.Split(script, @"(?im)^\s*GO\s*(?:--.*)?$"))
            if (!string.IsNullOrWhiteSpace(batch)) await ExecuteNonQueryAsync(connectionString, batch);
    }

    private static async Task ExecuteNonQueryAsync(string connectionString, string commandText)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(commandText, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SeedTopicPracticeQuestionAsync(string connectionString, string questionId, string answerId, string versionId, string tagId, string snapshot)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
            INSERT INTO dbo.Question (QuestionID, QuestionContent, SolutionContent, DifficultyID, Grade, Status, QuestionType, ExpertID, DefaultWeight, IsActive)
            VALUES (@questionId, @content, N'Solution', 'l3-difficulty-1', 12, 'Approved', 'SingleChoice', 'expert_l3_owner', 1.00, 1);
            INSERT INTO dbo.Answer (AnswerID, QuestionID, AnswerContent, IsCorrect)
            VALUES (@answerId, @questionId, N'Correct', 1);
            INSERT INTO dbo.QuestionTopic (QuestionTopicID, QuestionID, TagID, IsPrimary)
            VALUES (@questionTopicId, @questionId, @tagId, 1);
            INSERT INTO dbo.QuestionVersion (VersionID, QuestionID, QuestionContent, QuestionAnswer, AnswersSnapshot, VersionNumber, SnapshotSchemaVersion, ExpertID)
            VALUES (@versionId, @questionId, @content, N'Solution', @snapshot, 1, 2, 'expert_l3_owner');
            """, connection);
        command.Parameters.AddWithValue("@questionId", questionId);
        command.Parameters.AddWithValue("@answerId", answerId);
        command.Parameters.AddWithValue("@questionTopicId", $"{questionId}-topic");
        command.Parameters.AddWithValue("@versionId", versionId);
        command.Parameters.AddWithValue("@tagId", tagId);
        command.Parameters.AddWithValue("@content", $"L3 topic practice question {questionId}");
        command.Parameters.AddWithValue("@snapshot", snapshot);
        await command.ExecuteNonQueryAsync();
    }

    private static Task DropDatabaseIfExistsAsync(string masterConnectionString, string databaseName) => ExecuteNonQueryAsync(masterConnectionString, $$"""
        IF DB_ID(N'{{databaseName}}') IS NOT NULL
        BEGIN
            ALTER DATABASE [{{databaseName}}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
            DROP DATABASE [{{databaseName}}];
        END;
        """);
}

public sealed class BlueprintTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "BlueprintL3Test";
    public const string AccountHeader = "X-Test-Account-Id";
    public const string RoleHeader = "X-Test-Role";

    public BlueprintTestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var accountId = Request.Headers[AccountHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(accountId)) return Task.FromResult(AuthenticateResult.NoResult());
        var role = Request.Headers[RoleHeader].FirstOrDefault() ?? "Expert";
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, accountId), new Claim(ClaimTypes.Role, role)], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

public sealed class TestGenSqlServerFactAttribute : FactAttribute
{
    public TestGenSqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TESTGEN_SQLSERVER_CONNECTION")))
            Skip = "Set TESTGEN_SQLSERVER_CONNECTION to run the disposable SQL Server system tests.";
    }
}
