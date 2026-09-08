using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Xunit;

namespace MathInsight.Modules.QuestionBank.Tests.System;

public sealed class MentorFollowUpMigrationSqlServerTests
{
    private const string ConnectionEnvironmentVariable = "QUESTIONBANK_SQLSERVER_CONNECTION";

    [QuestionBankSqlServerFact]
    public async Task FreshAndRerun_PreservesSafeMappingAndAddsSnapshotForeignKeys()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        await database.ApplyAsync("001_Create_MathInsight_Azure.sql");
        await database.ApplyAsync("005_Align_TestGen_QuestionBank_Contract.sql");
        await database.SeedLegacyReportsAsync(includeInvalidSnapshotPointers: false, includeDuplicateReport: false);

        await database.ApplyAsync("006_MentorFollowUp_CompositePolicy.sql");
        var firstState = await database.ReadMigrationStateAsync();

        await database.ApplyAsync("006_MentorFollowUp_CompositePolicy.sql");
        var secondState = await database.ReadMigrationStateAsync();

        Assert.Equal(firstState, secondState);
        Assert.Equal("v-migration-safe", firstState.CanonicalVersionId);
        Assert.Null(firstState.DuplicateVersionId);
        Assert.Contains("VERSION_UNAVAILABLE", firstState.AuditIssueCodes);
        Assert.Equal(1, firstState.QuestionVersionForeignKeyCount);
        Assert.Equal(1, firstState.TestQuestionVersionForeignKeyCount);
        Assert.Equal(1, firstState.GradeRevisionDefaultCount);
    }

    [QuestionBankSqlServerFact]
    public async Task DuplicateReporterVersion_FailsWithoutMutatingHistoricalData()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        await database.ApplyAsync("001_Create_MathInsight_Azure.sql");
        await database.ApplyAsync("005_Align_TestGen_QuestionBank_Contract.sql");
        await database.SeedLegacyReportsAsync(includeInvalidSnapshotPointers: false, includeDuplicateReport: true);

        var exception = await Assert.ThrowsAsync<SqlException>(() => database.ApplyAsync("006_MentorFollowUp_CompositePolicy.sql"));

        Assert.Contains("duplicate reporter/version", exception.Message, StringComparison.OrdinalIgnoreCase);
        var state = await database.ReadPreMigrationStateAsync();
        Assert.Equal(3, state.ReportCount);
        Assert.Equal(0, state.QuestionReportQuestionVersionColumnCount);
        Assert.Equal(0, state.LegacyAuditTableCount);
    }

    [QuestionBankSqlServerFact]
    public async Task InvalidSnapshotPointer_RollsBackAndDoesNotAssignCrossQuestionVersion()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        await database.ApplyAsync("001_Create_MathInsight_Azure.sql");
        await database.ApplyAsync("005_Align_TestGen_QuestionBank_Contract.sql");
        await database.SeedLegacyReportsAsync(includeInvalidSnapshotPointers: true, includeDuplicateReport: false);

        var exception = await Assert.ThrowsAsync<SqlException>(() => database.ApplyAsync("006_MentorFollowUp_CompositePolicy.sql"));

        Assert.Contains("QuestionVersion", exception.Message, StringComparison.OrdinalIgnoreCase);
        var state = await database.ReadPreMigrationStateAsync();
        Assert.Equal(2, state.InvalidTestQuestionPointerCount);
        Assert.Equal(0, state.QuestionReportQuestionVersionColumnCount);
        Assert.Equal(0, state.LegacyAuditTableCount);
    }

    [QuestionBankSqlServerFact]
    public async Task ExistingIncompleteIncidentTable_FailsBeforeAnyMutation()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        await database.ApplyAsync("001_Create_MathInsight_Azure.sql");
        await database.ApplyAsync("005_Align_TestGen_QuestionBank_Contract.sql");
        await database.ExecuteAsync("CREATE TABLE dbo.QuestionReportIncident (IncidentID VARCHAR(36) NOT NULL CONSTRAINT PK_PartialIncident PRIMARY KEY);");

        var exception = await Assert.ThrowsAsync<SqlException>(() => database.ApplyAsync("006_MentorFollowUp_CompositePolicy.sql"));

        Assert.Contains("incomplete", exception.Message, StringComparison.OrdinalIgnoreCase);
        var state = await database.ReadPreMigrationStateAsync();
        Assert.Equal(1, state.PartialIncidentTableCount);
        Assert.Equal(0, state.TestQuestionGradingPolicyColumnCount);
        Assert.Equal(0, state.LegacyAuditTableCount);
    }

    [QuestionBankSqlServerFact]
    public async Task LegacyReportsWithoutSessionEvidence_AreAuditedAsVersionUnavailable()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        await database.ApplyAsync("001_Create_MathInsight_Azure.sql");
        await database.ApplyAsync("005_Align_TestGen_QuestionBank_Contract.sql");
        await database.SeedLegacyReportsAsync(includeInvalidSnapshotPointers: false, includeLegacySessionColumn: false);

        await database.ApplyAsync("006_MentorFollowUp_CompositePolicy.sql");

        Assert.Equal(3, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.QuestionReportLegacyAudit WHERE IssueCode = 'VERSION_UNAVAILABLE';"));
        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.QuestionReport WHERE QuestionVersionID IS NOT NULL;"));
    }

    [QuestionBankSqlServerFact]
    public async Task LegacyCompositePartCounts_AreClearedWithoutChangingPolicyOrOtherData()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        await database.ApplyAsync("001_Create_MathInsight_Azure.sql");
        await database.ApplyAsync("005_Align_TestGen_QuestionBank_Contract.sql");
        await database.SeedLegacyReportsAsync(includeInvalidSnapshotPointers: false, includeDuplicateReport: false);
        await database.SeedLegacyCompositeSectionsAsync();

        var before = await database.ReadLegacyCompositeStateAsync();

        await database.ApplyAsync("006_MentorFollowUp_CompositePolicy.sql");
        var after = await database.ReadLegacyCompositeStateAsync();

        Assert.Equal(54, before.LegacyPartCountRows);
        Assert.Equal(53, before.TieredRows);
        Assert.Equal(1, before.WeightedRows);
        Assert.Equal("AllOrNothing", before.TestQuestionScoringRuleSnapshot);
        Assert.Equal(1.00m, before.TestQuestionWeightSnapshot);
        Assert.Equal(1.00m, before.TestQuestionMaxPointsSnapshot);
        Assert.Equal("Safe question", before.QuestionVersionContent);
        Assert.Equal("[]", before.QuestionVersionAnswersSnapshot);
        Assert.Equal(0.00m, before.SessionScore);
        Assert.Equal(0, after.LegacyPartCountRows);
        Assert.Equal(53, after.TieredRows);
        Assert.Equal(1, after.WeightedRows);
        Assert.Equal(before.TotalQuestions, after.TotalQuestions);
        Assert.Equal(before.ScoreBudget, after.ScoreBudget);

        await database.ApplyAsync("006_MentorFollowUp_CompositePolicy.sql");
        Assert.Equal(after, await database.ReadLegacyCompositeStateAsync());
    }

    [QuestionBankSqlServerFact]
    public async Task ArchivedQuestionPartMigration_ReplacesLegacyUniquenessAndIsRerunnable()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        await database.ApplyAsync("001_Create_MathInsight_Azure.sql");
        await database.ApplyAsync("005_Align_TestGen_QuestionBank_Contract.sql");
        await database.SeedLegacyReportsAsync(includeInvalidSnapshotPointers: false, includeDuplicateReport: false);
        await database.ApplyAsync("006_MentorFollowUp_CompositePolicy.sql");

        // Recreate the database shape produced by the old 001/005 combination
        // so this regression tests the upgrade path, not only a clean schema.
        await database.ExecuteAsync("""
            DROP INDEX IF EXISTS UX_QuestionPart_Current_Order ON dbo.QuestionPart;
            DROP INDEX IF EXISTS UX_QuestionPart_Current_Label_NotNull ON dbo.QuestionPart;
            IF NOT EXISTS (
                SELECT 1 FROM sys.key_constraints
                WHERE name = N'UQ_QuestionPart_Question_Order'
                  AND parent_object_id = OBJECT_ID(N'dbo.QuestionPart'))
                ALTER TABLE dbo.QuestionPart
                    ADD CONSTRAINT UQ_QuestionPart_Question_Order UNIQUE (QuestionID, PartOrder);
            CREATE UNIQUE INDEX UX_QuestionPart_Label_NotNull
                ON dbo.QuestionPart (QuestionID, PartLabel)
                WHERE PartLabel IS NOT NULL;

            INSERT INTO dbo.Question
                (QuestionID, QuestionContent, SolutionContent, DifficultyID, Grade, Status,
                 QuestionType, ExpertID, DefaultPoint, IsActive, DefaultWeight, CreatedTime, UpdatedTime)
            VALUES
                ('migration-question-composite', N'Composite question', N'Composite solution',
                 'migration-difficulty', 10, 'Approved', 'Composite', 'migration-expert',
                 1.00, 1, 1.00, SYSUTCDATETIME(), SYSUTCDATETIME());

            INSERT INTO dbo.QuestionPart
                (PartID, QuestionID, PartOrder, PartLabel, PartContent, PartType,
                 CorrectBoolean, Explanation, DefaultPoint, DefaultWeight, IsArchived)
            VALUES
                ('migration-part-old-a', 'migration-question-composite', 1, N'a', N'Part a', 'TrueFalse',
                 1, N'Explanation a', 0.50, 1.00, 0),
                ('migration-part-old-b', 'migration-question-composite', 2, N'b', N'Part b', 'TrueFalse',
                 0, N'Explanation b', 0.50, 1.00, 0);
            """);

        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM sys.key_constraints WHERE name = 'UQ_QuestionPart_Question_Order';"));
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM sys.indexes WHERE name = 'UX_QuestionPart_Label_NotNull' AND object_id = OBJECT_ID('dbo.QuestionPart');"));

        await database.ApplyAsync("007_Fix_QuestionPart_Archived_Uniqueness.sql");

        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM sys.key_constraints WHERE name = 'UQ_QuestionPart_Question_Order';"));
        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM sys.indexes WHERE name = 'UX_QuestionPart_Label_NotNull' AND object_id = OBJECT_ID('dbo.QuestionPart');"));
        Assert.Equal(1, await database.ScalarAsync("""
            SELECT COUNT(*)
            FROM sys.indexes
            WHERE name = 'UX_QuestionPart_Current_Order'
              AND object_id = OBJECT_ID('dbo.QuestionPart')
              AND is_unique = 1
              AND has_filter = 1
              AND filter_definition LIKE '%IsArchived%0%';
            """));
        Assert.Equal(1, await database.ScalarAsync("""
            SELECT COUNT(*)
            FROM sys.indexes
            WHERE name = 'UX_QuestionPart_Current_Label_NotNull'
              AND object_id = OBJECT_ID('dbo.QuestionPart')
              AND is_unique = 1
              AND has_filter = 1
              AND filter_definition LIKE '%IsArchived%0%';
            """));

        await database.ExecuteAsync("""
            UPDATE dbo.QuestionPart
            SET IsArchived = 1
            WHERE PartID = 'migration-part-old-a';

            INSERT INTO dbo.QuestionPart
                (PartID, QuestionID, PartOrder, PartLabel, PartContent, PartType,
                 CorrectBoolean, Explanation, DefaultPoint, DefaultWeight, IsArchived)
            VALUES
                ('migration-part-new-a', 'migration-question-composite', 1, N'a', N'Updated part a', 'TrueFalse',
                 0, N'Updated explanation a', 0.50, 1.00, 0);
            """);

        var duplicateOrder = await Assert.ThrowsAsync<SqlException>(() => database.ExecuteAsync("""
            INSERT INTO dbo.QuestionPart
                (PartID, QuestionID, PartOrder, PartLabel, PartContent, PartType,
                 CorrectBoolean, Explanation, DefaultPoint, DefaultWeight, IsArchived)
            VALUES
                ('migration-part-duplicate-order', 'migration-question-composite', 1, N'c', N'Duplicate order', 'TrueFalse',
                 1, N'Duplicate order explanation', 0.50, 1.00, 0);
            """));
        Assert.Contains("UX_QuestionPart_Current_Order", duplicateOrder.Message, StringComparison.OrdinalIgnoreCase);

        var duplicateLabel = await Assert.ThrowsAsync<SqlException>(() => database.ExecuteAsync("""
            INSERT INTO dbo.QuestionPart
                (PartID, QuestionID, PartOrder, PartLabel, PartContent, PartType,
                 CorrectBoolean, Explanation, DefaultPoint, DefaultWeight, IsArchived)
            VALUES
                ('migration-part-duplicate-label', 'migration-question-composite', 3, N'a', N'Duplicate label', 'TrueFalse',
                 1, N'Duplicate label explanation', 0.50, 1.00, 0);
            """));
        Assert.Contains("UX_QuestionPart_Current_Label_NotNull", duplicateLabel.Message, StringComparison.OrdinalIgnoreCase);

        await database.ApplyAsync("007_Fix_QuestionPart_Archived_Uniqueness.sql");

        Assert.Equal(3, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.QuestionPart WHERE QuestionID = 'migration-question-composite';"));
        Assert.Equal(2, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.QuestionPart WHERE QuestionID = 'migration-question-composite' AND IsArchived = 0;"));
    }

    [QuestionBankSqlServerFact]
    public async Task InvalidCompositePolicy_RollsBackBeforeClearingLegacyPartCounts()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        await database.ApplyAsync("001_Create_MathInsight_Azure.sql");
        await database.ApplyAsync("005_Align_TestGen_QuestionBank_Contract.sql");
        await database.SeedLegacyReportsAsync(includeInvalidSnapshotPointers: false, includeDuplicateReport: false);
        await database.SeedLegacyCompositeSectionsAsync(includeInvalidPolicy: true);

        var exception = await Assert.ThrowsAsync<SqlException>(() => database.ApplyAsync("006_MentorFollowUp_CompositePolicy.sql"));

        Assert.Contains("BlueprintSection", exception.Message, StringComparison.OrdinalIgnoreCase);
        var state = await database.ReadLegacyCompositeStateAsync();
        Assert.Equal(55, state.LegacyPartCountRows);
        Assert.Equal(53, state.TieredRows);
        Assert.Equal(1, state.WeightedRows);
        Assert.Equal(1, state.InvalidPolicyRows);
        Assert.Equal(0, state.LegacyAuditTableCount);
    }

    [QuestionBankSqlServerFact]
    public async Task QuestionReportSessionVersionConstraint_IsCorrectOnFreshSchemaAndRerunnable()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        await database.ApplyAsync("001_Create_MathInsight_Azure.sql");
        await database.ApplyAsync("005_Align_TestGen_QuestionBank_Contract.sql");
        await database.SeedLegacyReportsAsync(includeInvalidSnapshotPointers: false, includeDuplicateReport: false);

        await database.ApplyAsync("006_MentorFollowUp_CompositePolicy.sql");
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM sys.check_constraints WHERE name = 'CK_QuestionReport_SessionVersionPair' AND parent_object_id = OBJECT_ID('dbo.QuestionReport');"));

        await database.InsertValidQuestionReportsAsync();

        var invalidSessionOnly = await Assert.ThrowsAsync<SqlException>(() => database.ExecuteAsync("""
            INSERT INTO dbo.QuestionReport
                (ReportID, QuestionID, ReporterAccountID, ReporterRole, ReportReason, Status, CreatedTime, SessionID)
            VALUES
                ('migration-invalid-session', 'migration-question-safe', 'migration-admin', 'Admin', N'Invalid session-only report', 'Pending', SYSUTCDATETIME(), 'migration-session-safe');
            """));
        Assert.Equal(547, invalidSessionOnly.Number);
        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.QuestionReport WHERE ReportID = 'migration-invalid-session';"));

        var reportCountBeforeRerun = await database.ScalarAsync("SELECT COUNT(*) FROM dbo.QuestionReport;");
        await database.ApplyAsync("007_Fix_QuestionPart_Archived_Uniqueness.sql");
        await database.ApplyAsync("008_Fix_QuestionReport_SessionVersion_Pair.sql");
        await database.ApplyAsync("008_Fix_QuestionReport_SessionVersion_Pair.sql");

        Assert.Equal(reportCountBeforeRerun, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.QuestionReport;"));
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM sys.check_constraints WHERE name = 'CK_QuestionReport_SessionVersionPair' AND parent_object_id = OBJECT_ID('dbo.QuestionReport');"));
    }

    [QuestionBankSqlServerFact]
    public async Task LegacySessionVersionConstraint_IsReplacedAndAllowsDirectReports()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        await database.ApplyAsync("001_Create_MathInsight_Azure.sql");
        await database.ApplyAsync("005_Align_TestGen_QuestionBank_Contract.sql");
        await database.SeedLegacyReportsAsync(includeInvalidSnapshotPointers: false, includeDuplicateReport: false);
        await database.ApplyAsync("006_MentorFollowUp_CompositePolicy.sql");
        await database.ApplyAsync("007_Fix_QuestionPart_Archived_Uniqueness.sql");

        await database.ReplaceSessionVersionConstraintAsync(legacy: true);
        var blockedBeforeUpgrade = await Assert.ThrowsAsync<SqlException>(() => database.InsertVersionOnlyReportAsync("migration-blocked-before"));
        Assert.Equal(547, blockedBeforeUpgrade.Number);
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM sys.check_constraints WHERE name = 'CK_QuestionReport_SessionVersionPair' AND parent_object_id = OBJECT_ID('dbo.QuestionReport') AND definition LIKE '%AND%';"));

        await database.ApplyAsync("008_Fix_QuestionReport_SessionVersion_Pair.sql");
        await database.InsertVersionOnlyReportAsync("migration-allowed-after");
        await database.ApplyAsync("008_Fix_QuestionReport_SessionVersion_Pair.sql");

        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.QuestionReport WHERE ReportID = 'migration-allowed-after';"));
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM sys.check_constraints WHERE name = 'CK_QuestionReport_SessionVersionPair' AND parent_object_id = OBJECT_ID('dbo.QuestionReport') AND definition LIKE '%SessionID%OR%QuestionVersionID%';"));
    }

    [QuestionBankSqlServerFact]
    public async Task ManuallyCorrectedSessionVersionConstraint_IsAcceptedAndRerunnable()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        await database.ApplyAsync("001_Create_MathInsight_Azure.sql");
        await database.ApplyAsync("005_Align_TestGen_QuestionBank_Contract.sql");
        await database.SeedLegacyReportsAsync(includeInvalidSnapshotPointers: false, includeDuplicateReport: false);
        await database.ApplyAsync("006_MentorFollowUp_CompositePolicy.sql");
        await database.ApplyAsync("007_Fix_QuestionPart_Archived_Uniqueness.sql");

        await database.ReplaceSessionVersionConstraintAsync(legacy: false);
        await database.ApplyAsync("008_Fix_QuestionReport_SessionVersion_Pair.sql");
        await database.InsertVersionOnlyReportAsync("migration-allowed-manual");
        await database.ApplyAsync("008_Fix_QuestionReport_SessionVersion_Pair.sql");

        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.QuestionReport WHERE ReportID = 'migration-allowed-manual';"));
    }

    [QuestionBankSqlServerFact]
    public async Task InvalidSessionOnlyReport_RollsBackConstraintUpgradeWithoutChangingData()
    {
        await using var database = await DisposableDatabase.CreateAsync();
        await database.ApplyAsync("001_Create_MathInsight_Azure.sql");
        await database.ApplyAsync("005_Align_TestGen_QuestionBank_Contract.sql");
        await database.SeedLegacyReportsAsync(includeInvalidSnapshotPointers: false, includeDuplicateReport: false);
        await database.ApplyAsync("006_MentorFollowUp_CompositePolicy.sql");
        await database.ApplyAsync("007_Fix_QuestionPart_Archived_Uniqueness.sql");
        await database.ExecuteAsync("ALTER TABLE dbo.QuestionReport DROP CONSTRAINT CK_QuestionReport_SessionVersionPair;");
        await database.ExecuteAsync("""
            INSERT INTO dbo.QuestionReport
                (ReportID, QuestionID, ReporterAccountID, ReporterRole, ReportReason, Status, CreatedTime, SessionID)
            VALUES
                ('migration-invalid-existing', 'migration-question-safe', 'migration-admin', 'Admin', N'Invalid existing report', 'Pending', SYSUTCDATETIME(), 'migration-session-safe');
            """);

        var exception = await Assert.ThrowsAsync<SqlException>(() => database.ApplyAsync("008_Fix_QuestionReport_SessionVersion_Pair.sql"));

        Assert.Contains("session-scoped", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.QuestionReport WHERE ReportID = 'migration-invalid-existing' AND SessionID IS NOT NULL AND QuestionVersionID IS NULL;"));
        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM sys.check_constraints WHERE name = 'CK_QuestionReport_SessionVersionPair' AND parent_object_id = OBJECT_ID('dbo.QuestionReport');"));
    }

    private sealed class DisposableDatabase : IAsyncDisposable
    {
        private readonly string _databaseName;
        private readonly string _masterConnectionString;
        private readonly string _connectionString;

        private DisposableDatabase(string databaseName, string masterConnectionString, string connectionString)
        {
            _databaseName = databaseName;
            _masterConnectionString = masterConnectionString;
            _connectionString = connectionString;
        }

        public static async Task<DisposableDatabase> CreateAsync()
        {
            var source = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(source))
                throw new InvalidOperationException($"{ConnectionEnvironmentVariable} is required.");

            var databaseName = $"MathInsightMigrationL3_{Guid.NewGuid():N}";
            var builder = new SqlConnectionStringBuilder(source);
            builder.InitialCatalog = "master";
            var masterConnectionString = builder.ConnectionString;
            builder.InitialCatalog = databaseName;
            var connectionString = builder.ConnectionString;

            await ExecuteAsync(masterConnectionString, $"CREATE DATABASE [{databaseName}];");
            return new DisposableDatabase(databaseName, masterConnectionString, connectionString);
        }

        public async Task ApplyAsync(string fileName)
        {
            var path = FindRepositoryFile("database", fileName);
            var script = await File.ReadAllTextAsync(path);
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            foreach (var batch in Regex.Split(script, @"(?im)^\s*GO\s*(?:--.*)?$"))
            {
                if (!string.IsNullOrWhiteSpace(batch))
                {
                    await using var command = new SqlCommand(batch, connection);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public Task ExecuteAsync(string sql) => ExecuteAsync(_connectionString, sql);

        public Task InsertVersionOnlyReportAsync(string reportId) => ExecuteAsync(_connectionString, $"""
            INSERT INTO dbo.QuestionReport
                (ReportID, QuestionID, ReporterAccountID, ReporterRole, ReportReason, Status, CreatedTime, QuestionVersionID)
            VALUES
                ('{reportId}', 'migration-question-safe', 'migration-expert', 'Expert', N'Version-only report', 'Pending', SYSUTCDATETIME(), 'v-migration-safe');
            """);

        public Task InsertValidQuestionReportsAsync() => ExecuteAsync(_connectionString, """
            INSERT INTO dbo.QuestionReport
                (ReportID, QuestionID, ReporterAccountID, ReporterRole, ReportReason, Status, CreatedTime, QuestionVersionID)
            VALUES
                ('migration-report-version-only', 'migration-question-safe', 'migration-expert', 'Expert', N'Valid direct report', 'Pending', SYSUTCDATETIME(), 'v-migration-safe');

            INSERT INTO dbo.QuestionReport
                (ReportID, QuestionID, ReporterAccountID, ReporterRole, ReportReason, Status, CreatedTime, SessionID, QuestionVersionID)
            VALUES
                ('migration-report-session-version', 'migration-question-safe', 'migration-admin', 'Admin', N'Valid session report', 'Pending', SYSUTCDATETIME(), 'migration-session-safe', 'v-migration-safe');

            INSERT INTO dbo.QuestionReport
                (ReportID, QuestionID, ReporterAccountID, ReporterRole, ReportReason, Status, CreatedTime)
            VALUES
                ('migration-report-legacy-null', 'migration-question-safe', 'migration-admin', 'Admin', N'Valid legacy report', 'Pending', SYSUTCDATETIME());
            """);

        public Task ReplaceSessionVersionConstraintAsync(bool legacy)
        {
            var predicate = legacy
                ? "([SessionID] IS NULL AND [QuestionVersionID] IS NULL) OR ([SessionID] IS NOT NULL AND [QuestionVersionID] IS NOT NULL)"
                : "[SessionID] IS NULL OR [QuestionVersionID] IS NOT NULL";
            return ExecuteAsync(_connectionString, $"""
                ALTER TABLE dbo.QuestionReport DROP CONSTRAINT CK_QuestionReport_SessionVersionPair;
                ALTER TABLE dbo.QuestionReport WITH CHECK ADD CONSTRAINT CK_QuestionReport_SessionVersionPair CHECK ({predicate});
                """);
        }

        public async Task<int> ScalarAsync(string sql)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        public async Task<MigrationState> ReadMigrationStateAsync()
        {
            const string sql = """
                SELECT
                    (SELECT QuestionVersionID FROM dbo.QuestionReport WHERE ReportID = 'migration-report-canonical') AS CanonicalVersionId,
                    (SELECT QuestionVersionID FROM dbo.QuestionReport WHERE ReportID = 'migration-report-duplicate') AS DuplicateVersionId,
                    (SELECT STRING_AGG(IssueCode, ',') WITHIN GROUP (ORDER BY IssueCode) FROM dbo.QuestionReportLegacyAudit) AS AuditIssueCodes,
                    (SELECT COUNT(*) FROM sys.foreign_keys WHERE name = 'FK_QuestionReport_QuestionVersion_QuestionVersionID') AS QuestionVersionForeignKeyCount,
                    (SELECT COUNT(*) FROM sys.foreign_keys WHERE name = 'FK_TestQuestion_QuestionVersion_QuestionVersionID') AS TestQuestionVersionForeignKeyCount,
                    (SELECT COUNT(*) FROM sys.default_constraints WHERE name = 'DF_StudentTopicSessionResult_GradeRevision') AS GradeRevisionDefaultCount;
                """;
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            return new MigrationState(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt32(5));
        }

        public async Task<PreMigrationState> ReadPreMigrationStateAsync()
        {
            const string sql = """
                SELECT
                    (SELECT COUNT(*) FROM dbo.TestQuestion tq LEFT JOIN dbo.QuestionVersion qv ON qv.VersionID = tq.QuestionVersionID WHERE tq.QuestionVersionID IS NOT NULL AND (qv.VersionID IS NULL OR qv.QuestionID <> tq.QuestionID)),
                    (SELECT COUNT(*) FROM dbo.QuestionReport),
                    (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.QuestionReport') AND name = 'QuestionVersionID'),
                    (SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID('dbo.QuestionReportLegacyAudit')),
                    (SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID('dbo.QuestionReportIncident')),
                    (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TestQuestion') AND name = 'GradingPolicyVersion'),
                    (SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID('dbo.QuestionReportLegacyAudit'));
                """;
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            return new PreMigrationState(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetInt32(6));
        }

        public async Task SeedLegacyReportsAsync(bool includeInvalidSnapshotPointers, bool includeDuplicateReport = true, bool includeLegacySessionColumn = true)
        {
            var invalidInsert = includeInvalidSnapshotPointers
                ? "INSERT INTO dbo.Test (TestID, TestStatus, TestMode, GeneratedBy, TestName, DurationMinutes, TotalQuestions) VALUES ('migration-test-invalid', 'Active', 'TopicPractice', 'System', N'Migration invalid test', 30, 2); INSERT INTO dbo.TestQuestion (TestID, QuestionID, QuestionOrder, SelectionReason, IsAdaptiveSelected, QuestionVersionID, WeightSnapshot, MaxPointsSnapshot, ScoringRuleSnapshot) VALUES ('migration-test-invalid', 'migration-question-safe', 1, 'TopicPractice', 0, 'migration-version-other-question', 1.00, 1.00, 'AllOrNothing'); INSERT INTO dbo.TestQuestion (TestID, QuestionID, QuestionOrder, SelectionReason, IsAdaptiveSelected, QuestionVersionID, WeightSnapshot, MaxPointsSnapshot, ScoringRuleSnapshot) VALUES ('migration-test-invalid', 'migration-question-orphan', 2, 'TopicPractice', 0, 'migration-version-missing', 1.00, 1.00, 'AllOrNothing');"
                : string.Empty;
            var reportSessionColumn = includeLegacySessionColumn ? ", SessionID" : string.Empty;
            var reportSessionValue = includeLegacySessionColumn ? ", 'migration-session-safe'" : string.Empty;
            var duplicateReportInsert = includeDuplicateReport
                ? $"INSERT INTO dbo.QuestionReport (ReportID, QuestionID, ReporterAccountID, ReporterRole, ReportReason, Status, CreatedTime{reportSessionColumn}) VALUES ('migration-report-duplicate', 'migration-question-safe', 'migration-student', 'Student', N'Duplicate report', 'Pending', DATEADD(SECOND, 2, SYSUTCDATETIME()){reportSessionValue});"
                : string.Empty;
            if (includeLegacySessionColumn)
                await ExecuteAsync(_connectionString, "ALTER TABLE dbo.QuestionReport ADD SessionID VARCHAR(36) NULL;");

            var sql = $"""
                INSERT INTO dbo.[Role] (RoleID, RoleName, Description) VALUES ('migration-role', N'Student', N'Migration test role');
                INSERT INTO dbo.Account (AccountID, Username, PasswordHash, Email, FirstName, LastName, RoleID, isActive)
                VALUES ('migration-student', N'migration-student', 'hash', 'migration-student@example.test', N'Migration', N'Student', 'migration-role', 1);
                INSERT INTO dbo.Student (StudentID, CurrentGrade) VALUES ('migration-student', 10);
                INSERT INTO dbo.Account (AccountID, Username, PasswordHash, Email, FirstName, LastName, RoleID, isActive)
                VALUES ('migration-expert', N'migration-expert', 'hash', 'migration-expert@example.test', N'Migration', N'Expert', 'migration-role', 1);
                INSERT INTO dbo.Account (AccountID, Username, PasswordHash, Email, FirstName, LastName, RoleID, isActive)
                VALUES ('migration-admin', N'migration-admin', 'hash', 'migration-admin@example.test', N'Migration', N'Admin', 'migration-role', 1);
                INSERT INTO dbo.Expert (ExpertID, Specialty) VALUES ('migration-expert', 'Migration test');
                INSERT INTO dbo.TagDifficulty (DifficultyID, DifficultyName, Description, LevelValue, DisplayOrder, IsActive)
                VALUES ('migration-difficulty', N'Migration difficulty', N'Migration test', 1, 1, 1);
                INSERT INTO dbo.Question (QuestionID, QuestionContent, SolutionContent, DifficultyID, Grade, Status, QuestionType, ExpertID, DefaultPoint, IsActive)
                VALUES ('migration-question-safe', N'Safe question', N'Safe solution', 'migration-difficulty', 10, 'Approved', 'SingleChoice', 'migration-expert', 1.00, 1);
                INSERT INTO dbo.Question (QuestionID, QuestionContent, SolutionContent, DifficultyID, Grade, Status, QuestionType, ExpertID, DefaultPoint, IsActive)
                VALUES ('migration-question-other', N'Other question', N'Other solution', 'migration-difficulty', 10, 'Approved', 'SingleChoice', 'migration-expert', 1.00, 1);
                INSERT INTO dbo.Question (QuestionID, QuestionContent, SolutionContent, DifficultyID, Grade, Status, QuestionType, ExpertID, DefaultPoint, IsActive)
                VALUES ('migration-question-orphan', N'Orphan question', N'Orphan solution', 'migration-difficulty', 10, 'Approved', 'SingleChoice', 'migration-expert', 1.00, 1);
                INSERT INTO dbo.QuestionVersion (VersionID, QuestionID, QuestionContent, QuestionAnswer, AnswersSnapshot, ExpertID)
                VALUES ('v-migration-safe', 'migration-question-safe', N'Safe question', N'Safe answer', N'[]', 'migration-expert');
                INSERT INTO dbo.QuestionVersion (VersionID, QuestionID, QuestionContent, QuestionAnswer, AnswersSnapshot, ExpertID)
                VALUES ('migration-version-other-question', 'migration-question-other', N'Other question', N'Other answer', N'[]', 'migration-expert');
                INSERT INTO dbo.Test (TestID, TestStatus, TestMode, GeneratedBy, TestName, DurationMinutes, TotalQuestions)
                VALUES ('migration-test-safe', 'Active', 'TopicPractice', 'System', N'Migration test', 30, 1);
                INSERT INTO dbo.TestSession (SessionID, TestID, StudentID, TestFormat, Status, SubmissionType, Duration, TotalQuestion, NumCorrect, NumIncorrect, NumAbandoned, Score)
                VALUES ('migration-session-safe', 'migration-test-safe', 'migration-student', 'Practice', 'Graded', 'StudentSubmit', 30, 1, 0, 1, 0, 0.00);
                INSERT INTO dbo.TestQuestion (TestID, QuestionID, QuestionOrder, SelectionReason, IsAdaptiveSelected, QuestionVersionID, WeightSnapshot, MaxPointsSnapshot, ScoringRuleSnapshot)
                VALUES ('migration-test-safe', 'migration-question-safe', 1, 'TopicPractice', 0, 'v-migration-safe', 1.00, 1.00, 'AllOrNothing');
                {invalidInsert}
                INSERT INTO dbo.QuestionReport (ReportID, QuestionID, ReporterAccountID, ReporterRole, ReportReason, Status, CreatedTime{reportSessionColumn})
                VALUES ('migration-report-canonical', 'migration-question-safe', 'migration-student', 'Student', N'Canonical report', 'Pending', DATEADD(SECOND, 1, SYSUTCDATETIME()){reportSessionValue});
                {duplicateReportInsert}
                INSERT INTO dbo.QuestionReport (ReportID, QuestionID, ReporterAccountID, ReporterRole, ReportReason, Status, CreatedTime)
                VALUES ('migration-report-unresolved', 'migration-question-safe', 'migration-student', 'Student', N'No snapshot evidence', 'Pending', DATEADD(SECOND, 3, SYSUTCDATETIME()));
                """;
            await ExecuteAsync(_connectionString, sql);
        }

        public async Task SeedLegacyCompositeSectionsAsync(bool includeInvalidPolicy = false)
        {
            var rows = new List<string>();
            for (var index = 1; index <= 53; index++)
            {
                rows.Add($"('migration-section-tiered-{index:00}', 'migration-blueprint', {index}, N'T{index:00}', N'Tiered {index:00}', 'Composite', 1, 1.00, 0.25, 4, 1.00, 'TieredTrueFalse')");
            }

            rows.Add("('migration-section-weighted', 'migration-blueprint', 54, N'W54', N'Weighted 54', 'Composite', 1, 1.00, 0.25, 4, 1.00, 'WeightedParts')");
            if (includeInvalidPolicy)
                rows.Add("('migration-section-invalid', 'migration-blueprint', 55, N'X55', N'Invalid 55', 'Composite', 1, 1.00, 0.25, 4, 1.00, 'AllOrNothing')");

            var sql = $"""
                INSERT INTO dbo.Blueprint (BlueprintID, BlueprintName, Grade, TotalQuestions, DurationMinutes, ExpertID, Status)
                VALUES ('migration-blueprint', N'Migration blueprint', 10, 54, 60, 'migration-expert', 'Draft');
                INSERT INTO dbo.BlueprintSection
                    (BlueprintSectionID, BlueprintID, SectionOrder, SectionCode, SectionName, QuestionType,
                     TotalQuestions, DefaultPointPerQuestion, DefaultPointPerPart, PartCountPerQuestion,
                     ScoreBudget, ScoringRule)
                VALUES {string.Join(",\n", rows)};
                """;
            await ExecuteAsync(_connectionString, sql);
        }

        public async Task<LegacyCompositeState> ReadLegacyCompositeStateAsync()
        {
            const string sql = """
                SELECT
                    COUNT(CASE WHEN PartCountPerQuestion IS NOT NULL THEN 1 END),
                    COUNT(CASE WHEN ScoringRule = 'TieredTrueFalse' THEN 1 END),
                    COUNT(CASE WHEN ScoringRule = 'WeightedParts' THEN 1 END),
                    COUNT(CASE WHEN ScoringRule NOT IN ('TieredTrueFalse', 'WeightedParts') THEN 1 END),
                    SUM(TotalQuestions),
                    SUM(ScoreBudget),
                    (SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID('dbo.QuestionReportLegacyAudit')),
                    (SELECT ScoringRuleSnapshot FROM dbo.TestQuestion WHERE TestID = 'migration-test-safe'),
                    (SELECT WeightSnapshot FROM dbo.TestQuestion WHERE TestID = 'migration-test-safe'),
                    (SELECT MaxPointsSnapshot FROM dbo.TestQuestion WHERE TestID = 'migration-test-safe'),
                    (SELECT QuestionContent FROM dbo.QuestionVersion WHERE VersionID = 'v-migration-safe'),
                    (SELECT AnswersSnapshot FROM dbo.QuestionVersion WHERE VersionID = 'v-migration-safe'),
                    (SELECT Score FROM dbo.TestSession WHERE SessionID = 'migration-session-safe')
                FROM dbo.BlueprintSection
                WHERE BlueprintID = 'migration-blueprint';
                """;
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            return new LegacyCompositeState(
                reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3),
                reader.GetInt32(4), reader.GetDecimal(5), reader.GetInt32(6), reader.GetString(7),
                reader.GetDecimal(8), reader.GetDecimal(9), reader.GetString(10), reader.GetString(11),
                reader.GetDecimal(12));
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await ExecuteAsync(_masterConnectionString, $"IF DB_ID(N'{_databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]; END;");
            }
            catch (SqlException)
            {
                // Preserve the original test failure if cleanup is blocked.
            }
        }

        private static async Task ExecuteAsync(string connectionString, string sql)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
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
    }

    private sealed record MigrationState(
        string CanonicalVersionId,
        string? DuplicateVersionId,
        string AuditIssueCodes,
        int QuestionVersionForeignKeyCount,
        int TestQuestionVersionForeignKeyCount,
        int GradeRevisionDefaultCount);

    private sealed record PreMigrationState(
        int InvalidTestQuestionPointerCount,
        int ReportCount,
        int QuestionReportQuestionVersionColumnCount,
        int LegacyAuditTableCount,
        int PartialIncidentTableCount,
        int TestQuestionGradingPolicyColumnCount,
        int LegacyAuditTableCountAfterFailure);

    private sealed record LegacyCompositeState(
        int LegacyPartCountRows,
        int TieredRows,
        int WeightedRows,
        int InvalidPolicyRows,
        int TotalQuestions,
        decimal ScoreBudget,
        int LegacyAuditTableCount,
        string TestQuestionScoringRuleSnapshot,
        decimal TestQuestionWeightSnapshot,
        decimal TestQuestionMaxPointsSnapshot,
        string QuestionVersionContent,
        string QuestionVersionAnswersSnapshot,
        decimal SessionScore);
}
