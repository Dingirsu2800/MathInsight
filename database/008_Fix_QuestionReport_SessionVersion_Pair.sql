/*
  008_Fix_QuestionReport_SessionVersion_Pair.sql
  Aligns the deployed QuestionReport session/version invariant with the
  application report contract.

  Direct Admin/Expert reports carry a QuestionVersionID without a SessionID.
  A session-scoped report must carry its immutable QuestionVersionID. Both
  NULL remains valid for historical reports that cannot be mapped safely.

  Apply after 007_Fix_QuestionPart_Archived_Uniqueness.sql. This script is
  rerunnable and must be executed as one transaction on the target database.
*/

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.QuestionReport', N'U') IS NULL
    THROW 50012, '008 cannot continue: dbo.QuestionReport does not exist.', 1;

IF COL_LENGTH(N'dbo.QuestionReport', N'SessionID') IS NULL
    THROW 50013, '008 cannot continue: dbo.QuestionReport.SessionID is missing; apply migration 006 first.', 1;

IF COL_LENGTH(N'dbo.QuestionReport', N'QuestionVersionID') IS NULL
    THROW 50014, '008 cannot continue: dbo.QuestionReport.QuestionVersionID is missing; apply migration 006 first.', 1;

IF EXISTS (
    SELECT 1
    FROM sys.objects
    WHERE name = N'CK_QuestionReport_SessionVersionPair'
      AND schema_id = SCHEMA_ID(N'dbo')
      AND parent_object_id <> OBJECT_ID(N'dbo.QuestionReport'))
    THROW 50015, '008 cannot continue: CK_QuestionReport_SessionVersionPair belongs to another object.', 1;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    -- Validate before dropping the old constraint so a failed upgrade leaves
    -- both data and the existing constraint untouched.
    IF EXISTS (
        SELECT 1
        FROM dbo.QuestionReport
        WHERE SessionID IS NOT NULL
          AND QuestionVersionID IS NULL)
        THROW 50016, '008 cannot continue: QuestionReport contains session-scoped reports without QuestionVersionID.', 1;

    IF EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = N'CK_QuestionReport_SessionVersionPair'
          AND parent_object_id = OBJECT_ID(N'dbo.QuestionReport'))
        ALTER TABLE dbo.QuestionReport DROP CONSTRAINT CK_QuestionReport_SessionVersionPair;

    ALTER TABLE dbo.QuestionReport WITH CHECK
        ADD CONSTRAINT CK_QuestionReport_SessionVersionPair CHECK (
            [SessionID] IS NULL OR [QuestionVersionID] IS NOT NULL);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
