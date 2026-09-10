/*
  L3 bootstrap contract alignment.
  Apply after 001_Create_MathInsight_Azure.sql.
  This script is idempotent and intentionally excludes demo data.
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.Blueprint', N'TotalScore') IS NULL
    ALTER TABLE dbo.Blueprint
        ADD TotalScore DECIMAL(5, 2) NOT NULL
            CONSTRAINT DF_Blueprint_TotalScore DEFAULT (10.00) WITH VALUES;

IF COL_LENGTH(N'dbo.BlueprintSection', N'ScoreBudget') IS NULL
    ALTER TABLE dbo.BlueprintSection
        ADD ScoreBudget DECIMAL(5, 2) NOT NULL
            CONSTRAINT DF_BlueprintSection_ScoreBudget DEFAULT (10.00) WITH VALUES;

IF COL_LENGTH(N'dbo.BlueprintSection', N'ScoringRule') IS NULL
    ALTER TABLE dbo.BlueprintSection
        ADD ScoringRule VARCHAR(30) NOT NULL
            CONSTRAINT DF_BlueprintSection_ScoringRule DEFAULT ('AllOrNothing') WITH VALUES;

IF COL_LENGTH(N'dbo.Question', N'DefaultWeight') IS NULL
    ALTER TABLE dbo.Question
        ADD DefaultWeight DECIMAL(5, 2) NOT NULL
            CONSTRAINT DF_Question_DefaultWeight DEFAULT (1.00) WITH VALUES;

IF COL_LENGTH(N'dbo.Question', N'CreatedTime') IS NULL
    ALTER TABLE dbo.Question
        ADD CreatedTime DATETIME2(0) NOT NULL
            CONSTRAINT DF_Question_CreatedTime DEFAULT (SYSUTCDATETIME()) WITH VALUES;

IF COL_LENGTH(N'dbo.Question', N'UpdatedTime') IS NULL
    ALTER TABLE dbo.Question
        ADD UpdatedTime DATETIME2(0) NOT NULL
            CONSTRAINT DF_Question_UpdatedTime DEFAULT (SYSUTCDATETIME()) WITH VALUES;

IF COL_LENGTH(N'dbo.Answer', N'IsArchived') IS NULL
    ALTER TABLE dbo.Answer
        ADD IsArchived BIT NOT NULL
            CONSTRAINT DF_Answer_IsArchived DEFAULT (0) WITH VALUES;

IF COL_LENGTH(N'dbo.QuestionPart', N'DefaultWeight') IS NULL
    ALTER TABLE dbo.QuestionPart
        ADD DefaultWeight DECIMAL(5, 2) NOT NULL
            CONSTRAINT DF_QuestionPart_DefaultWeight DEFAULT (1.00) WITH VALUES;

IF COL_LENGTH(N'dbo.QuestionPart', N'IsArchived') IS NULL
    ALTER TABLE dbo.QuestionPart
        ADD IsArchived BIT NOT NULL
            CONSTRAINT DF_QuestionPart_IsArchived DEFAULT (0) WITH VALUES;
GO

-- QuestionPart history is retained by soft-archiving old rows.  The original
-- 001 bootstrap constraint/indexes were not archive-aware, so replacing a
-- part with the same order or label failed on SQL Server.
IF EXISTS (
    SELECT 1
    FROM sys.key_constraints
    WHERE name = N'UQ_QuestionPart_Question_Order'
      AND parent_object_id = OBJECT_ID(N'dbo.QuestionPart'))
    ALTER TABLE dbo.QuestionPart DROP CONSTRAINT UQ_QuestionPart_Question_Order;

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_QuestionPart_Label_NotNull'
      AND object_id = OBJECT_ID(N'dbo.QuestionPart'))
    DROP INDEX UX_QuestionPart_Label_NotNull ON dbo.QuestionPart;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_QuestionPart_Current_Order'
      AND object_id = OBJECT_ID(N'dbo.QuestionPart'))
    CREATE UNIQUE INDEX UX_QuestionPart_Current_Order
        ON dbo.QuestionPart (QuestionID, PartOrder)
        WHERE IsArchived = 0;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_QuestionPart_Current_Label_NotNull'
      AND object_id = OBJECT_ID(N'dbo.QuestionPart'))
    CREATE UNIQUE INDEX UX_QuestionPart_Current_Label_NotNull
        ON dbo.QuestionPart (QuestionID, PartLabel)
        WHERE PartLabel IS NOT NULL AND IsArchived = 0;

IF COL_LENGTH(N'dbo.QuestionVersion', N'VersionNumber') IS NULL
    ALTER TABLE dbo.QuestionVersion
        ADD VersionNumber INT NOT NULL
            CONSTRAINT DF_QuestionVersion_VersionNumber DEFAULT (1) WITH VALUES;

IF COL_LENGTH(N'dbo.QuestionVersion', N'SnapshotSchemaVersion') IS NULL
    ALTER TABLE dbo.QuestionVersion
        ADD SnapshotSchemaVersion SMALLINT NOT NULL
            CONSTRAINT DF_QuestionVersion_SnapshotSchemaVersion DEFAULT (2) WITH VALUES;

IF COL_LENGTH(N'dbo.Test', N'MaxScore') IS NULL
    ALTER TABLE dbo.Test
        ADD MaxScore DECIMAL(5, 2) NOT NULL
            CONSTRAINT DF_Test_MaxScore DEFAULT (10.00) WITH VALUES;

IF COL_LENGTH(N'dbo.Test', N'ScoringPolicy') IS NULL
    ALTER TABLE dbo.Test
        ADD ScoringPolicy VARCHAR(30) NOT NULL
            CONSTRAINT DF_Test_ScoringPolicy DEFAULT ('BlueprintBudget') WITH VALUES;

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Test_DurationMinutes' AND parent_object_id = OBJECT_ID(N'dbo.Test'))
    ALTER TABLE dbo.Test DROP CONSTRAINT CK_Test_DurationMinutes;

ALTER TABLE dbo.Test WITH CHECK
    ADD CONSTRAINT CK_Test_DurationMinutes CHECK (DurationMinutes >= 0);

IF COL_LENGTH(N'dbo.TestQuestion', N'QuestionVersionID') IS NULL
    ALTER TABLE dbo.TestQuestion ADD QuestionVersionID VARCHAR(36) NULL;

IF COL_LENGTH(N'dbo.TestQuestion', N'WeightSnapshot') IS NULL
    ALTER TABLE dbo.TestQuestion
        ADD WeightSnapshot DECIMAL(5, 2) NOT NULL
            CONSTRAINT DF_TestQuestion_WeightSnapshot DEFAULT (1.00) WITH VALUES;

IF COL_LENGTH(N'dbo.TestQuestion', N'MaxPointsSnapshot') IS NULL
    ALTER TABLE dbo.TestQuestion
        ADD MaxPointsSnapshot DECIMAL(5, 2) NOT NULL
            CONSTRAINT DF_TestQuestion_MaxPointsSnapshot DEFAULT (1.00) WITH VALUES;

IF COL_LENGTH(N'dbo.TestQuestion', N'ScoringRuleSnapshot') IS NULL
    ALTER TABLE dbo.TestQuestion
        ADD ScoringRuleSnapshot VARCHAR(30) NOT NULL
            CONSTRAINT DF_TestQuestion_ScoringRuleSnapshot DEFAULT ('AllOrNothing') WITH VALUES;

IF COL_LENGTH(N'dbo.TestQuestion', N'IsScoreInvalidated') IS NULL
    ALTER TABLE dbo.TestQuestion
        ADD IsScoreInvalidated BIT NOT NULL
            CONSTRAINT DF_TestQuestion_IsScoreInvalidated DEFAULT (0) WITH VALUES;

IF COL_LENGTH(N'dbo.TestQuestion', N'InvalidatedByReportID') IS NULL
    ALTER TABLE dbo.TestQuestion ADD InvalidatedByReportID VARCHAR(36) NULL;

IF COL_LENGTH(N'dbo.TestSession', N'GradeRevision') IS NULL
    ALTER TABLE dbo.TestSession
        ADD GradeRevision INT NOT NULL
            CONSTRAINT DF_TestSession_GradeRevision DEFAULT (0) WITH VALUES;

COMMIT TRANSACTION;
