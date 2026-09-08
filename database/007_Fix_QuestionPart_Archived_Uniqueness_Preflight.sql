/*
  Read-only preflight for 007_Fix_QuestionPart_Archived_Uniqueness.sql.

  Azure already has 005 and 006. Run this script first, then apply only 007.
  Migration 007 executes its object replacement in one transaction. A
  precondition, active-duplicate, or index failure rolls the transaction back;
  it does not leave a partial index conversion.

  This script contains SELECTs and catalog reads only. It never changes data or
  schema. Do not use it as a substitute for the migration runner's -b/error
  handling.
*/

SET NOCOUNT ON;

SELECT
    N'007_Fix_QuestionPart_Archived_Uniqueness' AS [Migration],
    N'READ_ONLY' AS [ExecutionMode],
    N'005 and 006 are already applied; apply only 007 after this preflight.' AS [UpgradePath],
    N'007 uses one transaction; any failure rolls back the index conversion.' AS [FailureBehavior];

SELECT
    N'dbo.QuestionPart' AS [ObjectName],
    CASE WHEN OBJECT_ID(N'dbo.QuestionPart', N'U') IS NULL THEN N'NOT_FOUND' ELSE N'PRESENT' END AS [Status],
    CASE WHEN COL_LENGTH(N'dbo.QuestionPart', N'IsArchived') IS NULL THEN N'NOT_FOUND' ELSE N'PRESENT' END AS [IsArchivedColumn];

SELECT
    [ObjectName],
    [ObjectType],
    [Status],
    [IsUnique],
    [HasFilter],
    [FilterDefinition],
    [KeyColumns]
FROM
(
    SELECT
        N'UQ_QuestionPart_Question_Order' AS [ObjectName],
        N'UNIQUE_CONSTRAINT' AS [ObjectType],
        N'PRESENT' AS [Status],
        CAST(1 AS bit) AS [IsUnique],
        CAST(0 AS bit) AS [HasFilter],
        CAST(NULL AS nvarchar(max)) AS [FilterDefinition],
        CAST(NULL AS nvarchar(max)) AS [KeyColumns]
    WHERE EXISTS
    (
        SELECT 1
        FROM sys.key_constraints
        WHERE name = N'UQ_QuestionPart_Question_Order'
          AND parent_object_id = OBJECT_ID(N'dbo.QuestionPart')
    )

    UNION ALL

    SELECT
        N'UQ_QuestionPart_Question_Order',
        N'INDEX',
        N'PRESENT',
        i.is_unique,
        i.has_filter,
        i.filter_definition,
        (
            SELECT STRING_AGG(c.name, N',') WITHIN GROUP (ORDER BY ic.key_ordinal)
            FROM sys.index_columns AS ic
            INNER JOIN sys.columns AS c
                ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE ic.object_id = i.object_id
              AND ic.index_id = i.index_id
              AND ic.key_ordinal > 0
        )
    FROM sys.indexes AS i
    WHERE i.name = N'UQ_QuestionPart_Question_Order'
      AND i.object_id = OBJECT_ID(N'dbo.QuestionPart')

    UNION ALL

    SELECT
        N'UX_QuestionPart_Label_NotNull',
        N'INDEX',
        N'PRESENT',
        i.is_unique,
        i.has_filter,
        i.filter_definition,
        (
            SELECT STRING_AGG(c.name, N',') WITHIN GROUP (ORDER BY ic.key_ordinal)
            FROM sys.index_columns AS ic
            INNER JOIN sys.columns AS c
                ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE ic.object_id = i.object_id
              AND ic.index_id = i.index_id
              AND ic.key_ordinal > 0
        )
    FROM sys.indexes AS i
    WHERE i.name = N'UX_QuestionPart_Label_NotNull'
      AND i.object_id = OBJECT_ID(N'dbo.QuestionPart')

    UNION ALL

    SELECT
        N'UX_QuestionPart_Current_Order',
        N'INDEX',
        N'PRESENT',
        i.is_unique,
        i.has_filter,
        i.filter_definition,
        (
            SELECT STRING_AGG(c.name, N',') WITHIN GROUP (ORDER BY ic.key_ordinal)
            FROM sys.index_columns AS ic
            INNER JOIN sys.columns AS c
                ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE ic.object_id = i.object_id
              AND ic.index_id = i.index_id
              AND ic.key_ordinal > 0
        )
    FROM sys.indexes AS i
    WHERE i.name = N'UX_QuestionPart_Current_Order'
      AND i.object_id = OBJECT_ID(N'dbo.QuestionPart')

    UNION ALL

    SELECT
        N'UX_QuestionPart_Current_Label_NotNull',
        N'INDEX',
        N'PRESENT',
        i.is_unique,
        i.has_filter,
        i.filter_definition,
        (
            SELECT STRING_AGG(c.name, N',') WITHIN GROUP (ORDER BY ic.key_ordinal)
            FROM sys.index_columns AS ic
            INNER JOIN sys.columns AS c
                ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE ic.object_id = i.object_id
              AND ic.index_id = i.index_id
              AND ic.key_ordinal > 0
        )
    FROM sys.indexes AS i
    WHERE i.name = N'UX_QuestionPart_Current_Label_NotNull'
      AND i.object_id = OBJECT_ID(N'dbo.QuestionPart')
)
AS [Objects];

IF OBJECT_ID(N'dbo.QuestionPart', N'U') IS NULL
   OR COL_LENGTH(N'dbo.QuestionPart', N'IsArchived') IS NULL
BEGIN
    SELECT
        N'SCHEMA_READINESS' AS [CheckName],
        N'BLOCKED' AS [Status],
        N'Apply 005 before 007; no conversion was evaluated.' AS [Details];
END
ELSE
BEGIN
    DECLARE @activeDuplicateOrderSql nvarchar(max) = N'
        SELECT
            N''ACTIVE_DUPLICATE_ORDER'' AS [CheckName],
            CASE WHEN EXISTS
            (
                SELECT 1
                FROM dbo.QuestionPart
                WHERE IsArchived = 0
                GROUP BY QuestionID, PartOrder
                HAVING COUNT(*) > 1
            ) THEN N''BLOCKED'' ELSE N''READY'' END AS [Status],
            COUNT(*) AS [DuplicateGroupCount],
            COALESCE(SUM([DuplicateCount]), 0) AS [AffectedRowCount]
        FROM
        (
            SELECT COUNT(*) AS [DuplicateCount]
            FROM dbo.QuestionPart
            WHERE IsArchived = 0
            GROUP BY QuestionID, PartOrder
            HAVING COUNT(*) > 1
        ) AS [Duplicates];';
    EXEC sys.sp_executesql @activeDuplicateOrderSql;

    DECLARE @activeDuplicateLabelSql nvarchar(max) = N'
        SELECT
            N''ACTIVE_DUPLICATE_LABEL'' AS [CheckName],
            CASE WHEN EXISTS
            (
                SELECT 1
                FROM dbo.QuestionPart
                WHERE IsArchived = 0 AND PartLabel IS NOT NULL
                GROUP BY QuestionID, PartLabel
                HAVING COUNT(*) > 1
            ) THEN N''BLOCKED'' ELSE N''READY'' END AS [Status],
            COUNT(*) AS [DuplicateGroupCount],
            COALESCE(SUM([DuplicateCount]), 0) AS [AffectedRowCount]
        FROM
        (
            SELECT COUNT(*) AS [DuplicateCount]
            FROM dbo.QuestionPart
            WHERE IsArchived = 0 AND PartLabel IS NOT NULL
            GROUP BY QuestionID, PartLabel
            HAVING COUNT(*) > 1
        ) AS [Duplicates];';
    EXEC sys.sp_executesql @activeDuplicateLabelSql;

    DECLARE @archivedRowsSql nvarchar(max) = N'
        SELECT
            N''ARCHIVED_PARTS'' AS [CheckName],
            N''INFORMATIONAL'' AS [Status],
            COUNT(*) AS [ArchivedRowCount]
        FROM dbo.QuestionPart
        WHERE IsArchived = 1;';
    EXEC sys.sp_executesql @archivedRowsSql;
END;

SELECT
    N'LEGACY_INDEX_CONVERSION' AS [CheckName],
    CASE
        WHEN EXISTS
        (
            SELECT 1
            FROM sys.key_constraints
            WHERE name = N'UQ_QuestionPart_Question_Order'
              AND parent_object_id = OBJECT_ID(N'dbo.QuestionPart')
        )
        OR EXISTS
        (
            SELECT 1
            FROM sys.indexes
            WHERE name IN (N'UQ_QuestionPart_Question_Order', N'UX_QuestionPart_Label_NotNull')
              AND object_id = OBJECT_ID(N'dbo.QuestionPart')
        )
        OR NOT EXISTS
        (
            SELECT 1
            FROM sys.indexes
            WHERE name = N'UX_QuestionPart_Current_Order'
              AND object_id = OBJECT_ID(N'dbo.QuestionPart')
              AND is_unique = 1
              AND has_filter = 1
              AND filter_definition LIKE N'%IsArchived%0%'
        )
        OR NOT EXISTS
        (
            SELECT 1
            FROM sys.indexes
            WHERE name = N'UX_QuestionPart_Current_Label_NotNull'
              AND object_id = OBJECT_ID(N'dbo.QuestionPart')
              AND is_unique = 1
              AND has_filter = 1
              AND filter_definition LIKE N'%PartLabel%'
              AND filter_definition LIKE N'%IsArchived%0%'
        )
        THEN N'REQUIRED'
        ELSE N'ALREADY_APPLIED'
    END AS [Status],
    N'REQUIRED means 007 still has schema work to perform; it is not a data migration.' AS [Details];

SELECT
    N'POLICY_INTERPRETATION' AS [CheckName],
    N'007 preserves QuestionPart rows, ScoringRule, score budgets, snapshots and sessions; it only replaces legacy uniqueness with active-row filtered indexes.' AS [Details]
UNION ALL
SELECT
    N'PRODUCTION_SEQUENCE',
    N'Because Azure already has 005 and 006, run this SELECT-only preflight and then execute only 007 in one sqlcmd session with -b.';
