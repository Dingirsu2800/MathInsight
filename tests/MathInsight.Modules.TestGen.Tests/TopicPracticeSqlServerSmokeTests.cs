using System.Data.Common;
using System.Text.Json;
using System.Text.RegularExpressions;
using MathInsight.Modules.TestGen.Commands.GenerateTopicPractice;
using MathInsight.Modules.TestGen.Generation;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Modules.Testing.Commands.StartSession;
using MathInsight.Shared.Questions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TestGenContext = MathInsight.Modules.TestGen.Persistence.TestGenDbContext;
using TestingContext = MathInsight.Modules.Testing.Persistence.TestingDbContext;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class TopicPracticeSqlServerSmokeTests
{
    private const string ConnectionVariable = "TESTGEN_SQLSERVER_CONNECTION";

    [SqlServerSmokeFact(ConnectionVariable)]
    public async Task CurrentSchema_AmbiguousCommitPersistsOneUnlimitedOwnerOnlyPractice()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable(ConnectionVariable)!;
        var databaseName = $"MathInsightTopicPracticeSmoke_{Guid.NewGuid():N}";
        var masterConnectionString = BuildConnectionString(baseConnectionString, "master");
        var databaseConnectionString = BuildConnectionString(baseConnectionString, databaseName);

        await CreateDatabaseAsync(masterConnectionString, databaseName);
        try
        {
            await ApplyCurrentSchemaAsync(databaseConnectionString);
            await SeedScenarioAsync(databaseConnectionString);

            var ambiguousCommit = new ThrowOnceAfterCommitInterceptor();
            await using var generationContext = CreateTestGenContext(databaseConnectionString, ambiguousCommit);
            var handler = new GenerateTopicPracticeCommandHandler(
                generationContext,
                new QuestionCandidateCatalog(generationContext),
                new TopicPracticeQuestionSelector(new NoOpRandomizer()));

            var generation = await handler.Handle(
                new GenerateTopicPracticeCommand("student_01", "TOPIC-G12-CALCULUS"),
                CancellationToken.None);

            Assert.True(ambiguousCommit.WasTriggered);
            Assert.True(generation.IsSuccess);
            generationContext.ChangeTracker.Clear();
            var persisted = await generationContext.Tests
                .AsNoTracking()
                .Include(test => test.Questions)
                .SingleAsync(test => test.TestId == generation.Value!.TestId);
            Assert.True(TopicPracticePersistenceVerifier.IsValid(
                persisted,
                "student_01",
                "TOPIC-G12-CALCULUS",
                "Luyện tập: Giải tích 12"));
            Assert.Equal(1, await generationContext.Tests.CountAsync());
            Assert.Equal(10, await generationContext.TestQuestions.CountAsync());

            await using var testingContext = CreateTestingContext(databaseConnectionString);
            var ownerStart = await new StartSessionCommandHandler(testingContext).Handle(
                new StartSessionCommand(persisted.TestId, "student_01"),
                CancellationToken.None);
            var otherStart = await new StartSessionCommandHandler(testingContext).Handle(
                new StartSessionCommand(persisted.TestId, "student_02"),
                CancellationToken.None);

            Assert.True(ownerStart.IsSuccess);
            Assert.Equal("Practice", ownerStart.Value!.TestFormat);
            Assert.False(ownerStart.Value.HasTimeLimit);
            Assert.Null(ownerStart.Value.RemainingSeconds);
            Assert.True(otherStart.IsFailure);
            Assert.Equal("TESTING_TEST_ACCESS_DENIED", otherStart.Error!.Code);
        }
        finally
        {
            await DropDatabaseAsync(masterConnectionString, databaseName);
        }
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

    private static async Task ApplyCurrentSchemaAsync(string connectionString)
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
        const string baseSeed = """
            INSERT INTO [Role] ([RoleID], [RoleName])
            VALUES ('role-expert', N'Expert'), ('role-student', N'Student');

            INSERT INTO [Account]
                ([AccountID], [Username], [PasswordHash], [Email], [FirstName], [LastName], [RoleID], [isActive])
            VALUES
                ('expert_01', N'expert_01', 'hash', 'expert01@smoke.local', N'Expert', N'One', 'role-expert', 1),
                ('student_01', N'student_01', 'hash', 'student01@smoke.local', N'Student', N'One', 'role-student', 1),
                ('student_02', N'student_02', 'hash', 'student02@smoke.local', N'Student', N'Two', 'role-student', 1);

            INSERT INTO [Expert] ([ExpertID], [Specialty]) VALUES ('expert_01', 'Mathematics');
            INSERT INTO [Student] ([StudentID], [CurrentGrade]) VALUES ('student_01', 12), ('student_02', 12);

            INSERT INTO [TagTopic] ([TagID], [TagName], [Grade], [IsActive], [DisplayOrder])
            VALUES ('TOPIC-G12-CALCULUS', N'Giải tích 12', 12, 1, 1);

            INSERT INTO [TagDifficulty]
                ([DifficultyID], [DifficultyName], [LevelValue], [DisplayOrder], [IsActive])
            VALUES
                ('DIFF-1', N'Nhận biết', 1, 1, 1),
                ('DIFF-2', N'Thông hiểu', 2, 2, 1),
                ('DIFF-3', N'Vận dụng', 3, 3, 1),
                ('DIFF-4', N'Vận dụng cao', 4, 4, 1);
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = baseSeed;
            await command.ExecuteNonQueryAsync();
        }

        for (var index = 1; index <= 12; index++)
        {
            var questionId = $"QUESTION-{index:00}";
            var answerId = $"ANSWER-{index:00}";
            var versionId = $"VERSION-{index:00}";
            var difficultyId = $"DIFF-{((index - 1) % 4) + 1}";
            var snapshot = new QuestionSnapshotV2(
                questionId,
                "SingleChoice",
                difficultyId,
                12,
                1m,
                [new QuestionTopicSnapshot("TOPIC-G12-CALCULUS", true)],
                [new QuestionAnswerSnapshot(answerId, "Đáp án đúng", true)],
                [],
                $"Câu hỏi {index}",
                "Lời giải");

            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO [Question]
                    ([QuestionID], [QuestionContent], [SolutionContent], [DifficultyID], [Grade], [Status],
                     [QuestionType], [ExpertID], [DefaultWeight], [IsActive])
                VALUES (@questionId, @content, N'Lời giải', @difficultyId, 12, 'Approved',
                        'SingleChoice', 'expert_01', 1.00, 1);

                INSERT INTO [Answer] ([AnswerID], [QuestionID], [AnswerContent], [IsCorrect])
                VALUES (@answerId, @questionId, N'Đáp án đúng', 1);

                INSERT INTO [QuestionTopic] ([QuestionTopicID], [QuestionID], [TagID], [IsPrimary])
                VALUES (@questionTopicId, @questionId, 'TOPIC-G12-CALCULUS', 1);

                INSERT INTO [QuestionVersion]
                    ([VersionID], [QuestionID], [QuestionContent], [QuestionAnswer], [AnswersSnapshot],
                     [VersionNumber], [SnapshotSchemaVersion], [ExpertID])
                VALUES (@versionId, @questionId, @content, N'Lời giải', @snapshot, 1, 2, 'expert_01');
                """;
            command.Parameters.AddWithValue("@questionId", questionId);
            command.Parameters.AddWithValue("@answerId", answerId);
            command.Parameters.AddWithValue("@questionTopicId", $"QUESTION-TOPIC-{index:00}");
            command.Parameters.AddWithValue("@versionId", versionId);
            command.Parameters.AddWithValue("@difficultyId", difficultyId);
            command.Parameters.AddWithValue("@content", $"Câu hỏi {index}");
            command.Parameters.AddWithValue("@snapshot", JsonSerializer.Serialize(snapshot));
            await command.ExecuteNonQueryAsync();
        }
    }

    private static TestGenContext CreateTestGenContext(
        string connectionString,
        DbTransactionInterceptor interceptor)
    {
        var options = new DbContextOptionsBuilder<TestGenContext>()
            .UseSqlServer(
                connectionString,
                sqlOptions => sqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(1), null))
            .AddInterceptors(interceptor)
            .Options;
        return new TestGenContext(options);
    }

    private static TestingContext CreateTestingContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<TestingContext>()
            .UseSqlServer(
                connectionString,
                sqlOptions => sqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(1), null))
            .Options;
        return new TestingContext(options);
    }

    private sealed class NoOpRandomizer : IGenerationRandomizer
    {
        public void Shuffle<T>(IList<T> values)
        {
        }
    }

    private sealed class ThrowOnceAfterCommitInterceptor : DbTransactionInterceptor
    {
        private int _remainingThrows = 1;
        public bool WasTriggered { get; private set; }

        public override Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _remainingThrows, 0) == 1)
            {
                WasTriggered = true;
                throw new TimeoutException("Simulated lost acknowledgement after a successful commit.");
            }

            return Task.CompletedTask;
        }
    }
}
