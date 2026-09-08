/*
  007_Fix_QuestionPart_Archived_Uniqueness.sql
  Makes QuestionPart replacement compatible with the soft-archive history
  contract used by the application.

  The 001 bootstrap schema created unfiltered uniqueness for PartOrder and
  PartLabel.  UpdateQuestion archives the current rows and inserts the new
  rows in one transaction, so those legacy constraints reject valid edits.
  This migration preserves all rows and enforces uniqueness only among active
  parts.

  Apply after 005_Align_TestGen_QuestionBank_Contract.sql.  This script is
  idempotent and must be executed as one transaction on the target database.
*/

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.QuestionPart', N'U') IS NULL
    THROW 50008, '007 cannot continue: dbo.QuestionPart does not exist.', 1;

IF COL_LENGTH(N'dbo.QuestionPart', N'IsArchived') IS NULL
    THROW 50009, '007 cannot continue: dbo.QuestionPart.IsArchived is missing; apply migration 005 first.', 1;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF EXISTS (
        SELECT 1
        FROM dbo.QuestionPart
        WHERE IsArchived = 0
        GROUP BY QuestionID, PartOrder
        HAVING COUNT(*) > 1)
        THROW 50010, '007 cannot continue: active QuestionPart rows contain duplicate QuestionID/PartOrder values.', 1;

    IF EXISTS (
        SELECT 1
        FROM dbo.QuestionPart
        WHERE IsArchived = 0 AND PartLabel IS NOT NULL
        GROUP BY QuestionID, PartLabel
        HAVING COUNT(*) > 1)
        THROW 50011, '007 cannot continue: active QuestionPart rows contain duplicate QuestionID/PartLabel values.', 1;

    IF EXISTS (
        SELECT 1
        FROM sys.key_constraints
        WHERE name = N'UQ_QuestionPart_Question_Order'
          AND parent_object_id = OBJECT_ID(N'dbo.QuestionPart'))
        ALTER TABLE dbo.QuestionPart DROP CONSTRAINT UQ_QuestionPart_Question_Order;

    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'UQ_QuestionPart_Question_Order'
          AND object_id = OBJECT_ID(N'dbo.QuestionPart'))
        DROP INDEX UQ_QuestionPart_Question_Order ON dbo.QuestionPart;

    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'UX_QuestionPart_Label_NotNull'
          AND object_id = OBJECT_ID(N'dbo.QuestionPart'))
        DROP INDEX UX_QuestionPart_Label_NotNull ON dbo.QuestionPart;

    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'UX_QuestionPart_Current_Order'
          AND object_id = OBJECT_ID(N'dbo.QuestionPart')
          AND NOT (
              is_unique = 1
              AND has_filter = 1
              AND filter_definition LIKE N'%IsArchived%0%'
              AND EXISTS (
                  SELECT 1
                  FROM sys.index_columns AS ic
                  INNER JOIN sys.columns AS c
                      ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                  WHERE ic.object_id = OBJECT_ID(N'dbo.QuestionPart')
                    AND ic.index_id = sys.indexes.index_id
                    AND ic.key_ordinal = 1
                    AND c.name = N'QuestionID')
              AND EXISTS (
                  SELECT 1
                  FROM sys.index_columns AS ic
                  INNER JOIN sys.columns AS c
                      ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                  WHERE ic.object_id = OBJECT_ID(N'dbo.QuestionPart')
                    AND ic.index_id = sys.indexes.index_id
                    AND ic.key_ordinal = 2
                    AND c.name = N'PartOrder')))
        DROP INDEX UX_QuestionPart_Current_Order ON dbo.QuestionPart;

    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'UX_QuestionPart_Current_Label_NotNull'
          AND object_id = OBJECT_ID(N'dbo.QuestionPart')
          AND NOT (
              is_unique = 1
              AND has_filter = 1
              AND filter_definition LIKE N'%PartLabel%'
              AND filter_definition LIKE N'%IsArchived%0%'
              AND EXISTS (
                  SELECT 1
                  FROM sys.index_columns AS ic
                  INNER JOIN sys.columns AS c
                      ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                  WHERE ic.object_id = OBJECT_ID(N'dbo.QuestionPart')
                    AND ic.index_id = sys.indexes.index_id
                    AND ic.key_ordinal = 1
                    AND c.name = N'QuestionID')
              AND EXISTS (
                  SELECT 1
                  FROM sys.index_columns AS ic
                  INNER JOIN sys.columns AS c
                      ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                  WHERE ic.object_id = OBJECT_ID(N'dbo.QuestionPart')
                    AND ic.index_id = sys.indexes.index_id
                    AND ic.key_ordinal = 2
                    AND c.name = N'PartLabel')))
        DROP INDEX UX_QuestionPart_Current_Label_NotNull ON dbo.QuestionPart;

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

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
