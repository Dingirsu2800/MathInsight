using System.Text.RegularExpressions;
using MathInsight.Modules.Recommender.Persistence;
using MathInsight.Modules.Recommender.Queries.GetRecommendedLectures;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MathInsight.Modules.Recommender.Tests.Integration;

public sealed class LectureRecommendationSqlServerSmokeTests
{
    private const string ConnectionEnvironmentVariable = "RECOMMENDER_SQLSERVER_CONNECTION";

    [SqlServerFact]
    public async Task Handle_CanonicalSchema_SupportsPersonalizedAndColdStartRecommendations()
    {
        var sourceConnectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable)!;

        var databaseName = $"MathInsightLectureRecommendationSmoke_{Guid.NewGuid():N}";
        var masterConnectionString = WithDatabase(sourceConnectionString, "master");
        var databaseConnectionString = WithDatabase(sourceConnectionString, databaseName);

        try
        {
            await ExecuteNonQueryAsync(masterConnectionString, $"CREATE DATABASE [{databaseName}]");
            await ExecuteSqlScriptAsync(databaseConnectionString, FindCanonicalSchemaPath());
            await MakeLectureTableLegacyAsync(databaseConnectionString);
            await ExecuteSqlScriptAsync(databaseConnectionString, FindLectureDifficultyMigrationPath());
            await ExecuteSqlScriptAsync(databaseConnectionString, FindLectureDifficultyMigrationPath());
            await SeedRequiredRowsAsync(databaseConnectionString);

            var options = new DbContextOptionsBuilder<RecommenderDbContext>()
                .UseSqlServer(databaseConnectionString)
                .Options;

            await using var db = new RecommenderDbContext(options);
            var handler = new GetRecommendedLecturesQueryHandler(db);

            var personalized = await handler.Handle(
                new GetRecommendedLecturesQuery("student_01"),
                CancellationToken.None);

            Assert.Equal(new[] { "lecture-personalized-l3", "lecture-personalized-l2" },
                personalized.Select(x => x.LectureId));
            Assert.All(personalized, x => Assert.True(x.DifficultyLevel <= x.TargetDifficultyLevel));

            var coldStart = await handler.Handle(
                new GetRecommendedLecturesQuery("student_cold"),
                CancellationToken.None);

            var coldLecture = Assert.Single(coldStart);
            Assert.Equal("lecture-foundation-l1", coldLecture.LectureId);
            Assert.Equal("ColdStartGradeFoundation", coldLecture.Reason);
        }
        finally
        {
            await DropDatabaseIfExistsAsync(masterConnectionString, databaseName);
        }
    }

    internal static async Task SeedRequiredRowsAsync(string connectionString)
    {
        const string script = """
            INSERT INTO dbo.[Role] (RoleID, RoleName, Description) VALUES
                ('role-student', N'Student', N'Smoke test role'),
                ('role-teacher', N'Teacher', N'Smoke test role');

            INSERT INTO dbo.Account (AccountID, Username, PasswordHash, Email, FirstName, LastName, RoleID, isActive) VALUES
                ('student_01', N'student_01', 'hash', 'student_01@example.test', N'Student', N'One', 'role-student', 1),
                ('student_cold', N'student_cold', 'hash', 'student_cold@example.test', N'Student', N'Cold', 'role-student', 1),
                ('teacher_01', N'teacher_01', 'hash', 'teacher_01@example.test', N'Teacher', N'One', 'role-teacher', 1);

            INSERT INTO dbo.Student (StudentID, CurrentGrade) VALUES
                ('student_01', 12),
                ('student_cold', 12);

            INSERT INTO dbo.Teacher (TeacherID, isVerified) VALUES ('teacher_01', 1);

            INSERT INTO dbo.TagDifficulty (DifficultyID, DifficultyName, LevelValue, DisplayOrder, IsActive) VALUES
                ('diff-l1', N'Level 1', 1, 1, 1),
                ('diff-l2', N'Level 2', 2, 2, 1),
                ('diff-l3', N'Level 3', 3, 3, 1),
                ('diff-l4', N'Level 4', 4, 4, 1);

            INSERT INTO dbo.TagTopic (TagID, TagName, Grade, IsActive, DisplayOrder) VALUES
                ('topic-personalized', N'Personalized topic', 12, 1, 1),
                ('topic-foundation', N'Foundation topic', 12, 1, 2);

            INSERT INTO dbo.TagsMastery
                (TagsMasteryID, StudentID, TagID, OfficialPoint, PracticePoint, ExamAnchor, ExamHistory,
                 SeriesAnswerCount, RecommendedDifficultyLevel, MasteryStatus, NumberDone, NumCorrect, AccuracyRate)
            VALUES
                ('mastery-student-topic', 'student_01', 'topic-personalized', 4.00, 4.00, 4.00, N'[]',
                 0, 3, 'Learning', 3, 2, 66.67);

            INSERT INTO dbo.Lecture
                (LectureID, Title, Content, VideoUrl, ThumbnailUrl, Likes, TeacherID, TagID, DifficultyID, Status, CreatedTime, UpdatedTime)
            VALUES
                ('lecture-personalized-l2', N'Lower level', N'content', NULL, NULL, 10, 'teacher_01', 'topic-personalized', 'diff-l2', 'Published', SYSUTCDATETIME(), SYSUTCDATETIME()),
                ('lecture-personalized-l3', N'Exact level', N'content', NULL, NULL, 1, 'teacher_01', 'topic-personalized', 'diff-l3', 'Published', SYSUTCDATETIME(), SYSUTCDATETIME()),
                ('lecture-personalized-l4', N'Harder level', N'content', NULL, NULL, 99, 'teacher_01', 'topic-personalized', 'diff-l4', 'Published', SYSUTCDATETIME(), SYSUTCDATETIME()),
                ('lecture-foundation-l1', N'Foundation level', N'content', NULL, NULL, 4, 'teacher_01', 'topic-foundation', 'diff-l1', 'Published', SYSUTCDATETIME(), SYSUTCDATETIME());
            """;

        await ExecuteNonQueryAsync(connectionString, script);
    }

    private static Task MakeLectureTableLegacyAsync(string connectionString)
        => ExecuteNonQueryAsync(connectionString, """
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Lecture_TagDifficulty_DifficultyID')
                ALTER TABLE dbo.Lecture DROP CONSTRAINT FK_Lecture_TagDifficulty_DifficultyID;
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Lecture') AND name = N'IX_Lecture_Status_TagID_DifficultyID')
                DROP INDEX IX_Lecture_Status_TagID_DifficultyID ON dbo.Lecture;
            IF COL_LENGTH(N'dbo.Lecture', N'DifficultyID') IS NOT NULL
                ALTER TABLE dbo.Lecture DROP COLUMN DifficultyID;
            """);

    internal static async Task ExecuteSqlScriptAsync(string connectionString, string scriptPath)
    {
        var script = await File.ReadAllTextAsync(scriptPath);
        var batches = Regex.Split(script, @"(?im)^\s*GO\s*(?:--.*)?$");

        foreach (var batch in batches)
        {
            if (!string.IsNullOrWhiteSpace(batch))
                await ExecuteNonQueryAsync(connectionString, batch);
        }
    }

    internal static async Task ExecuteNonQueryAsync(string connectionString, string commandText)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(commandText, connection);
        await command.ExecuteNonQueryAsync();
    }

    internal static async Task DropDatabaseIfExistsAsync(string masterConnectionString, string databaseName)
    {
        var command = $"""
            IF DB_ID(N'{databaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{databaseName}];
            END;
            """;

        await ExecuteNonQueryAsync(masterConnectionString, command);
    }

    internal static string WithDatabase(string connectionString, string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = databaseName
        };

        return builder.ConnectionString;
    }

    internal static string FindCanonicalSchemaPath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Database", "database", "001_Create_MathInsight_Azure.sql");
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException("Canonical database schema was not found.");
    }

    internal static string FindLectureDifficultyMigrationPath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Database", "Migrations", "006_Lecture_Difficulty_Recommendation.sql");
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException("Lecture difficulty migration was not found.");
    }
}

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RECOMMENDER_SQLSERVER_CONNECTION")))
            Skip = "Set RECOMMENDER_SQLSERVER_CONNECTION to run the disposable SQL Server smoke test.";
    }
}
