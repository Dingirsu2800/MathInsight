using System.Text.RegularExpressions;
using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Commands.ReviewBlueprint;
using MathInsight.Modules.TestGen.Commands.SubmitBlueprintForReview;
using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Modules.TestGen.Validation;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class BlueprintSqlServerSmokeTests
{
    private const string ConnectionVariable = "TESTGEN_SQLSERVER_CONNECTION";

    [SqlServerSmokeFact(ConnectionVariable)]
    public async Task CurrentSchema_EnforcesCompositeFkAndSerializesSubmitReview()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable(ConnectionVariable)!;
        var databaseName = $"MathInsightTestGenSmoke_{Guid.NewGuid():N}";
        var masterConnectionString = BuildConnectionString(baseConnectionString, "master");
        var databaseConnectionString = BuildConnectionString(baseConnectionString, databaseName);

        await CreateDatabaseAsync(masterConnectionString, databaseName);
        try
        {
            await ApplyCurrentSchemaAsync(databaseConnectionString);
            await SeedBlueprintScenarioAsync(databaseConnectionString);
            await VerifyCompositeForeignKeyAsync(databaseConnectionString);
            await VerifyConcurrentSubmitReviewAsync(databaseConnectionString);
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
        var schemaPath = Path.Combine(
            AppContext.BaseDirectory,
            "Database",
            "001_Create_MathInsight_Azure.sql");
        var script = await File.ReadAllTextAsync(schemaPath);
        var batches = Regex.Split(
            script,
            @"^\s*GO\s*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

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

    private static async Task SeedBlueprintScenarioAsync(string connectionString)
    {
        const string seedSql = """
            INSERT INTO [Role] ([RoleID], [RoleName])
            VALUES ('role-expert', N'Expert');

            INSERT INTO [Account]
                ([AccountID], [Username], [PasswordHash], [Email], [FirstName], [LastName], [RoleID], [isActive])
            VALUES
                ('smoke-owner', N'smoke-owner', 'hash', 'owner@smoke.local', N'Owner', N'Expert', 'role-expert', 1),
                ('smoke-reviewer', N'smoke-reviewer', 'hash', 'reviewer@smoke.local', N'Reviewer', N'Expert', 'role-expert', 1);

            INSERT INTO [Expert] ([ExpertID], [Specialty])
            VALUES
                ('smoke-owner', 'Mathematics'),
                ('smoke-reviewer', 'Mathematics');

            INSERT INTO [TagTopic]
                ([TagID], [TagName], [Grade], [IsActive], [DisplayOrder])
            VALUES ('smoke-topic', N'Smoke Topic', 12, 1, 1);

            INSERT INTO [TagDifficulty]
                ([DifficultyID], [DifficultyName], [LevelValue], [DisplayOrder], [IsActive])
            VALUES ('smoke-difficulty', N'Smoke Difficulty', 1, 1, 1);

            INSERT INTO [Blueprint]
                ([BlueprintID], [BlueprintName], [Grade], [TotalQuestions], [DurationMinutes], [ExpertID], [Status])
            VALUES
                ('smoke-blueprint', N'Smoke Blueprint', 12, 1, 15, 'smoke-owner', 'Draft'),
                ('other-blueprint', N'Other Blueprint', 12, 1, 15, 'smoke-owner', 'Draft');

            INSERT INTO [BlueprintSection]
                ([BlueprintSectionID], [BlueprintID], [SectionOrder], [SectionName], [QuestionType],
                 [TotalQuestions], [DefaultPointPerQuestion])
            VALUES
                ('smoke-section', 'smoke-blueprint', 1, N'Section I', 'SingleChoice', 1, 1.00),
                ('other-section', 'other-blueprint', 1, N'Section I', 'SingleChoice', 1, 1.00);

            INSERT INTO [BlueprintDetail]
                ([BlueprintDetailID], [BlueprintID], [BlueprintSectionID], [TagID], [DifficultyID], [Quantity])
            VALUES
                ('smoke-detail', 'smoke-blueprint', 'smoke-section', 'smoke-topic', 'smoke-difficulty', 1);
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = seedSql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task VerifyCompositeForeignKeyAsync(string connectionString)
    {
        await using var context = CreateContext(connectionString);
        context.BlueprintDetails.Add(new BlueprintDetail
        {
            BlueprintDetailId = "invalid-composite-detail",
            BlueprintId = "smoke-blueprint",
            BlueprintSectionId = "other-section",
            TagId = "smoke-topic",
            DifficultyId = "smoke-difficulty",
            Quantity = 1
        });

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());
        var sqlException = Assert.IsType<SqlException>(exception.InnerException);
        Assert.Equal(547, sqlException.Number);
    }

    private static async Task VerifyConcurrentSubmitReviewAsync(string connectionString)
    {
        await using var submitContext = CreateContext(connectionString);
        await using var reviewContext = CreateContext(connectionString);
        var submitHandler = new SubmitBlueprintForReviewCommandHandler(
            submitContext,
            new BlueprintAggregateValidator(submitContext));
        var reviewHandler = new ReviewBlueprintCommandHandler(reviewContext);

        var submitTask = submitHandler.Handle(
            new SubmitBlueprintForReviewCommand("smoke-blueprint", "smoke-owner"),
            CancellationToken.None);
        var reviewTask = reviewHandler.Handle(
            new ReviewBlueprintCommand(
                "smoke-blueprint",
                new ReviewBlueprintRequest { Action = BlueprintReviewActions.Approve },
                "smoke-reviewer"),
            CancellationToken.None);

        await Task.WhenAll(submitTask, reviewTask);

        Assert.True(submitTask.Result.IsSuccess);
        if (reviewTask.Result.IsFailure)
            Assert.Equal(BlueprintErrors.StatusInvalid, reviewTask.Result.Error);

        await using var assertionContext = CreateContext(connectionString);
        var persisted = await assertionContext.Blueprints
            .AsNoTracking()
            .SingleAsync(blueprint => blueprint.BlueprintId == "smoke-blueprint");
        Assert.Contains(
            persisted.Status,
            new[] { BlueprintStatuses.PendingReview, BlueprintStatuses.Approved });
        if (persisted.Status == BlueprintStatuses.Approved)
            Assert.Equal("smoke-reviewer", persisted.ApprovedBy);
    }

    private static TestGenDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<TestGenDbContext>()
            .UseSqlServer(
                connectionString,
                sqlOptions => sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(1),
                    errorNumbersToAdd: null))
            .Options;
        return new TestGenDbContext(options);
    }
}

internal sealed class SqlServerSmokeFactAttribute : FactAttribute
{
    public SqlServerSmokeFactAttribute(string connectionVariable)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(connectionVariable)))
        {
            Skip = $"Set {connectionVariable} to a disposable SQL Server master connection.";
        }
    }
}
