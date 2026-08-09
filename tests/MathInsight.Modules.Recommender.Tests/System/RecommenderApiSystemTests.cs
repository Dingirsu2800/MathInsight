using System.Security.Claims;
using System.Text.Encodings.Web;
using MathInsight.Modules.Recommender.Persistence;
using MathInsight.Modules.Recommender.Persistence.Entities;
using MathInsight.Modules.Recommender.Tests.Integration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace MathInsight.Modules.Recommender.Tests.System;

public sealed class RecommenderApiSystemTests : IClassFixture<RecommenderApiFactory>
{
    private readonly HttpClient _client;
    private readonly RecommenderApiFactory _factory;

    public RecommenderApiSystemTests(RecommenderApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetWeakTags_WithoutBearerToken_ReturnsUnauthorizedFromHostedApi()
    {
        var response = await _client.GetAsync("/api/v1/recommender/weak-tags");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetWeakTags_WithAuthenticatedStudent_ReturnsOnlyThatStudentsWeakTags()
    {
        await _factory.SeedAsync(db =>
        {
            db.TagTopics.AddRange(
                new TagTopicReadOnly { TagId = "weak", TagName = "Weak", Grade = 10, IsActive = true },
                new TagTopicReadOnly { TagId = "strong", TagName = "Strong", Grade = 10, IsActive = true });
            db.TagsMasteries.AddRange(
                new TagsMastery { TagsMasteryId = "m1", StudentId = "student_01", TagId = "weak", OfficialPoint = 2m, PracticePoint = 2m, ExamAnchor = 2m, MasteryStatus = "Learning", RecommendedDifficultyLevel = 1, ExamHistory = "[]" },
                new TagsMastery { TagsMasteryId = "m2", StudentId = "student_01", TagId = "strong", OfficialPoint = 8m, PracticePoint = 8m, ExamAnchor = 8m, MasteryStatus = "Mastered", RecommendedDifficultyLevel = 1, ExamHistory = "[]" });
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/recommender/weak-tags");
        request.Headers.Add("X-Test-Student-Id", "student_01");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("weak", await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain("strong", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetLectures_WithAuthenticatedStudent_ReturnsPublishedWeakTopicLecture()
    {
        await _factory.SeedAsync(db =>
        {
            db.TagTopics.Add(new TagTopicReadOnly { TagId = "topic-lecture", TagName = "Algebra", Grade = 10, IsActive = true });
            db.TagsMasteries.Add(new TagsMastery { TagsMasteryId = "m3", StudentId = "student_01", TagId = "topic-lecture", OfficialPoint = 2m, PracticePoint = 2m, ExamAnchor = 2m, MasteryStatus = "Learning", NumberDone = 3, RecommendedDifficultyLevel = 1, ExamHistory = "[]" });
        });
        await _factory.AddLectureAsync("lecture-1", "Algebra basics", "topic-lecture", "diff-l1");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/recommender/lectures");
        request.Headers.Add("X-Test-Student-Id", "student_01");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Algebra basics", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetMaterials_WithAuthenticatedStudent_ReturnsMaterialLinkedToRecommendedLecture()
    {
        await _factory.SeedAsync(db =>
        {
            db.TagTopics.Add(new TagTopicReadOnly { TagId = "topic-material", TagName = "Geometry", Grade = 10, IsActive = true });
            db.TagsMasteries.Add(new TagsMastery { TagsMasteryId = "m4", StudentId = "student_01", TagId = "topic-material", OfficialPoint = 2m, PracticePoint = 2m, ExamAnchor = 2m, MasteryStatus = "Learning", NumberDone = 3, RecommendedDifficultyLevel = 1, ExamHistory = "[]" });
        });
        await _factory.AddLectureAsync("lecture-2", "Geometry basics", "topic-material", "diff-l1");
        await _factory.AddMaterialAsync("material-1", "Geometry worksheet");
        await _factory.LinkMaterialAsync("lecture-2", "material-1");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/recommender/materials");
        request.Headers.Add("X-Test-Student-Id", "student_01");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Geometry worksheet", await response.Content.ReadAsStringAsync());
    }
}

public sealed class RecommenderApiFactory : WebApplicationFactory<Program>
{
    private const string SqlConnectionEnvironmentVariable = "RECOMMENDER_SQLSERVER_CONNECTION";
    private readonly string _databaseName = Guid.NewGuid().ToString("N");
    private readonly string? _masterConnectionString;
    private readonly string? _sqlConnectionString;

    public RecommenderApiFactory()
    {
        var sourceConnectionString = Environment.GetEnvironmentVariable(SqlConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(sourceConnectionString))
            return;

        _masterConnectionString = LectureRecommendationSqlServerSmokeTests.WithDatabase(sourceConnectionString, "master");
        _sqlConnectionString = LectureRecommendationSqlServerSmokeTests.WithDatabase(sourceConnectionString, $"MathInsightRecommenderApiL3_{_databaseName}");
        LectureRecommendationSqlServerSmokeTests.ExecuteNonQueryAsync(_masterConnectionString, $"CREATE DATABASE [MathInsightRecommenderApiL3_{_databaseName}]").GetAwaiter().GetResult();
        LectureRecommendationSqlServerSmokeTests.ExecuteSqlScriptAsync(_sqlConnectionString, LectureRecommendationSqlServerSmokeTests.FindCanonicalSchemaPath()).GetAwaiter().GetResult();
        LectureRecommendationSqlServerSmokeTests.ExecuteSqlScriptAsync(_sqlConnectionString, LectureRecommendationSqlServerSmokeTests.FindLectureDifficultyMigrationPath()).GetAwaiter().GetResult();
        LectureRecommendationSqlServerSmokeTests.SeedRequiredRowsAsync(_sqlConnectionString).GetAwaiter().GetResult();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        if (_sqlConnectionString is not null)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = _sqlConnectionString
                }));
        }
        builder.ConfigureServices(services =>
        {
            if (_sqlConnectionString is null)
            {
                services.RemoveAll<DbContextOptions<RecommenderDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<RecommenderDbContext>>();
                services.RemoveAll<RecommenderDbContext>();
                services.AddDbContext<RecommenderDbContext>(options => options.UseInMemoryDatabase(_databaseName));
            }
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    public async Task SeedAsync(Action<RecommenderDbContext> seed)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecommenderDbContext>();
        if (_sqlConnectionString is null)
        {
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
            db.TagDifficulties.Add(new TagDifficultyReadOnly
            {
                DifficultyId = "diff-l1",
                DifficultyName = "Level 1",
                LevelValue = 1,
                IsActive = true
            });
        }
        seed(db);
        await db.SaveChangesAsync();
    }

    public async Task AddLectureAsync(string lectureId, string title, string tagId, string difficultyId)
    {
        if (_sqlConnectionString is not null)
        {
            await LectureRecommendationSqlServerSmokeTests.ExecuteNonQueryAsync(_sqlConnectionString,
                $"INSERT INTO dbo.Lecture (LectureID, Title, Content, Likes, TeacherID, TagID, DifficultyID, Status, CreatedTime, UpdatedTime) VALUES ('{lectureId}', N'{title}', N'test', 0, 'teacher_01', '{tagId}', '{difficultyId}', 'Published', SYSUTCDATETIME(), SYSUTCDATETIME())");
            return;
        }

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecommenderDbContext>();
        db.Lectures.Add(new LectureReadOnly { LectureId = lectureId, Title = title, TagId = tagId, DifficultyId = difficultyId, Status = "Published" });
        await db.SaveChangesAsync();
    }

    public async Task LinkMaterialAsync(string lectureId, string materialId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecommenderDbContext>();
        db.LectureMaterials.Add(new LectureMaterialReadOnly { LectureId = lectureId, MaterialId = materialId });
        await db.SaveChangesAsync();
    }

    public async Task AddMaterialAsync(string materialId, string materialName)
    {
        if (_sqlConnectionString is not null)
        {
            await LectureRecommendationSqlServerSmokeTests.ExecuteNonQueryAsync(_sqlConnectionString,
                $"INSERT INTO dbo.Material (MaterialID, MaterialName, FileUrl, FileType, TeacherID, Status, UploadedTime) VALUES ('{materialId}', N'{materialName}', 'https://example.test/{materialId}.pdf', 'pdf', 'teacher_01', 'Active', SYSUTCDATETIME())");
            return;
        }

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecommenderDbContext>();
        db.Materials.Add(new MaterialReadOnly { MaterialId = materialId, MaterialName = materialName, FileUrl = $"https://example.test/{materialId}.pdf", FileType = "pdf", Status = "Active" });
        await db.SaveChangesAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (_masterConnectionString is not null)
            LectureRecommendationSqlServerSmokeTests.DropDatabaseIfExistsAsync(_masterConnectionString, $"MathInsightRecommenderApiL3_{_databaseName}").GetAwaiter().GetResult();
    }
}

public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "L3Test";
    public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var studentId = Request.Headers["X-Test-Student-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(studentId)) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, studentId), new Claim(ClaimTypes.Role, "Student") }, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
