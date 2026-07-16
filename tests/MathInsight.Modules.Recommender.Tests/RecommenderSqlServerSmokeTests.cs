using System.Diagnostics;
using System.Text.RegularExpressions;
using MathInsight.Modules.Recommender.Handlers;
using MathInsight.Modules.Recommender.Persistence;
using MathInsight.Modules.Recommender.Queries.GetRecommendedLectures;
using MathInsight.Modules.Recommender.Queries.GetRecommendedMaterials;
using MathInsight.Modules.Recommender.Services;
using MathInsight.Shared.Events;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MathInsight.Modules.Recommender.Tests;

public sealed class RecommenderSqlServerSmokeTests
{
    private const string ConnectionVariable = "RECOMMENDER_SQLSERVER_CONNECTION";

    [RecommenderSqlServerSmokeFact(ConnectionVariable)]
    public async Task CanonicalSchema_SupportsSemanticIdsIngestionAndRecommendations()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable(ConnectionVariable)!;
        var databaseName = $"MathInsightRecommenderSmoke_{Guid.NewGuid():N}";
        var masterConnectionString = BuildConnectionString(baseConnectionString, "master");
        var databaseConnectionString = BuildConnectionString(baseConnectionString, databaseName);

        await CreateDatabaseAsync(masterConnectionString, databaseName);
        try
        {
            await ApplyCanonicalSchemaAsync(databaseConnectionString);
            await SeedScenarioAsync(databaseConnectionString);
            await VerifyIngestionAndSequentialIdempotencyAsync(databaseConnectionString);
            await VerifyConcurrentIdempotencyAsync(databaseConnectionString);
            await VerifyRecommendationQueriesAsync(databaseConnectionString);
            await VerifyWeakTagSlaAsync(databaseConnectionString);
        }
        finally
        {
            await DropDatabaseAsync(masterConnectionString, databaseName);
        }
    }

    private static async Task VerifyIngestionAndSequentialIdempotencyAsync(string connectionString)
    {
        await using var context = CreateContext(connectionString);
        var handler = CreateHandler(context);
        var evt = ExamEvent("session_01");

        await handler.Handle(evt, default);
        await handler.Handle(evt, default);

        context.ChangeTracker.Clear();
        var snapshot = Assert.Single(await context.StudentTopicSessionResults.ToListAsync());
        Assert.Equal("student_01", snapshot.StudentId);
        Assert.Equal("TOPIC-G12-DERIVAPP", snapshot.TagId);
        Assert.Equal(0m, snapshot.EarnedPoints);
        Assert.Equal(1m, snapshot.MaxPoints);

        var mastery = Assert.Single(await context.TagsMasteries.ToListAsync());
        Assert.Equal(1.50m, mastery.OfficialPoint);
        Assert.Equal(0m, mastery.AccuracyRate);

        var competency = Assert.Single(await context.CompetencyPoints.ToListAsync());
        Assert.Equal(12, competency.Grade);
        Assert.Equal(1.50m, competency.Point);
    }

    private static async Task VerifyConcurrentIdempotencyAsync(string connectionString)
    {
        await using var firstContext = CreateContext(connectionString);
        await using var secondContext = CreateContext(connectionString);
        var evt = ExamEvent("session_02");

        await Task.WhenAll(
            CreateHandler(firstContext).Handle(evt, default),
            CreateHandler(secondContext).Handle(evt, default));

        await using var assertionContext = CreateContext(connectionString);
        Assert.Equal(
            1,
            await assertionContext.StudentTopicSessionResults.CountAsync(
                result => result.SessionId == "session_02" &&
                          result.TagId == "TOPIC-G12-DERIVAPP"));
        var mastery = await assertionContext.TagsMasteries.SingleAsync(
            item => item.StudentId == "student_01" &&
                    item.TagId == "TOPIC-G12-DERIVAPP");
        Assert.Equal(2, mastery.NumberDone);
    }

    private static async Task VerifyRecommendationQueriesAsync(string connectionString)
    {
        await using var context = CreateContext(connectionString);
        var mapping = new DifficultyMappingService();
        var weakTags = await new RecommenderService(context, mapping)
            .GetStudentWeakTagsAsync("student_01");
        var lectures = await new GetRecommendedLecturesQueryHandler(context, mapping)
            .Handle(new GetRecommendedLecturesQuery("student_01"), default);
        var materials = await new GetRecommendedMaterialsQueryHandler(context, mapping)
            .Handle(new GetRecommendedMaterialsQuery("student_01"), default);

        Assert.Equal("TOPIC-G12-DERIVAPP", Assert.Single(weakTags).TagId);
        Assert.Equal("lecture_published", Assert.Single(lectures).LectureId);
        Assert.Equal("material_active", Assert.Single(materials).MaterialId);
    }

    private static async Task VerifyWeakTagSlaAsync(string connectionString)
    {
        await SeedPerformanceMasteriesAsync(connectionString);
        await using var context = CreateContext(connectionString);
        var service = new RecommenderService(context, new DifficultyMappingService());

        var stopwatch = Stopwatch.StartNew();
        var weakTags = await service.GetStudentWeakTagsAsync("student_01");
        stopwatch.Stop();

        Assert.True(weakTags.Count >= 50);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(2),
            $"WeakTag SQL query took {stopwatch.Elapsed.TotalMilliseconds:F0} ms.");
    }

    private static TopicResultIngestionHandler CreateHandler(RecommenderDbContext context)
        => new(context, new CompetencyEngine(context));

    private static GradeCalculatedEvent ExamEvent(string sessionId)
        => new()
        {
            SessionId = sessionId,
            StudentId = "student_01",
            TestId = "test_01",
            TestFormat = "Exam",
            GradedAt = DateTime.UtcNow,
            PerTagResults =
            [
                new TopicGradeResult
                {
                    TagId = "TOPIC-G12-DERIVAPP",
                    TotalItems = 1m,
                    CorrectItems = 0m,
                    EarnedPoints = 0m,
                    MaxPoints = 1m,
                    TopicScore = 0m
                }
            ]
        };

    private static RecommenderDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<RecommenderDbContext>()
            .UseSqlServer(
                connectionString,
                sqlOptions => sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(2),
                    errorNumbersToAdd: [1205]))
            .Options;
        return new RecommenderDbContext(options);
    }

    private static string BuildConnectionString(string connectionString, string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = databaseName,
            TrustServerCertificate = true,
            Encrypt = false
        };
        return builder.ConnectionString;
    }

    private static async Task CreateDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{databaseName}]";
        command.CommandTimeout = 60;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF DB_ID(N'{databaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{databaseName}];
            END;
            """;
        command.CommandTimeout = 60;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ApplyCanonicalSchemaAsync(string connectionString)
    {
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Database", "001_Create_MathInsight_Azure.sql");
        var script = await File.ReadAllTextAsync(schemaPath);
        var batches = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        foreach (var batch in batches.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = batch;
            command.CommandTimeout = 120;
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task SeedScenarioAsync(string connectionString)
    {
        const string sql = """
            INSERT INTO [Role] ([RoleID], [RoleName])
            VALUES ('role-student', N'Student'), ('role-teacher', N'Teacher');

            INSERT INTO [Account]
                ([AccountID], [Username], [PasswordHash], [Email], [FirstName], [LastName], [RoleID], [isActive])
            VALUES
                ('student_01', N'student_01', 'hash', 'student@smoke.local', N'Smoke', N'Student', 'role-student', 1),
                ('teacher_01', N'teacher_01', 'hash', 'teacher@smoke.local', N'Smoke', N'Teacher', 'role-teacher', 1);

            INSERT INTO [Student] ([StudentID], [CurrentGrade]) VALUES ('student_01', 12);
            INSERT INTO [Teacher] ([TeacherID]) VALUES ('teacher_01');
            INSERT INTO [TagTopic] ([TagID], [TagName], [Grade], [IsActive], [DisplayOrder])
            VALUES ('TOPIC-G12-DERIVAPP', N'Derivative applications smoke', 12, 1, 1);

            INSERT INTO [Test]
                ([TestID], [TestMode], [GeneratedForStudentID], [GeneratedBy], [TestName], [DurationMinutes], [TotalQuestions])
            VALUES ('test_01', 'TopicPractice', 'student_01', 'System', N'Smoke Test', 15, 1);

            INSERT INTO [TestSession]
                ([SessionID], [TestID], [StudentID], [TestFormat], [Status], [SubmissionType], [TotalQuestion], [NumCorrect])
            VALUES
                ('session_01', 'test_01', 'student_01', 'Exam', 'Graded', 'StudentSubmit', 1, 1),
                ('session_02', 'test_01', 'student_01', 'Exam', 'Graded', 'StudentSubmit', 1, 1);

            INSERT INTO [Lecture]
                ([LectureID], [Title], [Content], [TeacherID], [TagID], [Status])
            VALUES
                ('lecture_published', N'Published lecture', N'Content', 'teacher_01', 'TOPIC-G12-DERIVAPP', 'Published'),
                ('lecture_draft', N'Draft lecture', N'Content', 'teacher_01', 'TOPIC-G12-DERIVAPP', 'Draft');

            INSERT INTO [Material]
                ([MaterialID], [MaterialName], [FileUrl], [FileType], [TeacherID], [Status])
            VALUES
                ('material_active', N'Active material', 'https://example.test/a.pdf', 'pdf', 'teacher_01', 'Active'),
                ('material_inactive', N'Inactive material', 'https://example.test/b.pdf', 'pdf', 'teacher_01', 'Deactivated');

            INSERT INTO [LectureMaterial] ([LectureID], [MaterialID])
            VALUES
                ('lecture_published', 'material_active'),
                ('lecture_published', 'material_inactive');
            """;

        await ExecuteSqlAsync(connectionString, sql);
    }

    private static async Task SeedPerformanceMasteriesAsync(string connectionString)
    {
        const string sql = """
            ;WITH Numbers AS
            (
                SELECT TOP (50) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS Number
                FROM sys.all_objects
            )
            INSERT INTO [TagTopic] ([TagID], [TagName], [Grade], [IsActive], [DisplayOrder])
            SELECT
                CONCAT('PERF-TOPIC-', FORMAT(Number, '00')),
                CONCAT(N'Performance topic ', FORMAT(Number, '00')),
                12,
                1,
                Number + 1
            FROM Numbers;

            ;WITH Numbers AS
            (
                SELECT TOP (50) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS Number
                FROM sys.all_objects
            )
            INSERT INTO [TagsMastery]
                ([TagsMasteryID], [StudentID], [TagID], [OfficialPoint], [PracticePoint], [ExamAnchor],
                 [RecommendedDifficultyLevel], [MasteryStatus], [NumberDone], [NumCorrect], [AccuracyRate])
            SELECT
                CONCAT('PERF-MASTERY-', FORMAT(Number, '00')),
                'student_01',
                CONCAT('PERF-TOPIC-', FORMAT(Number, '00')),
                4.00,
                4.00,
                4.00,
                2,
                'Learning',
                1,
                0,
                0.00
            FROM Numbers;
            """;

        await ExecuteSqlAsync(connectionString, sql);
    }

    private static async Task ExecuteSqlAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 60;
        await command.ExecuteNonQueryAsync();
    }
}

internal sealed class RecommenderSqlServerSmokeFactAttribute : FactAttribute
{
    public RecommenderSqlServerSmokeFactAttribute(string connectionVariable)
    {
        var connectionString = Environment.GetEnvironmentVariable(connectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Skip = $"Set {connectionVariable} to a disposable SQL Server master connection.";
            return;
        }

        var dataSource = new SqlConnectionStringBuilder(connectionString).DataSource;
        if (dataSource.Contains(".database.windows.net", StringComparison.OrdinalIgnoreCase))
            Skip = $"{connectionVariable} must not target Azure or a shared database server.";
    }
}
