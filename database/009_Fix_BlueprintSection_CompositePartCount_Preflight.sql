SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- READ-ONLY PREFLIGHT for 009_Fix_BlueprintSection_CompositePartCount.sql.
-- Run this against the target database before applying 009. It does not
-- create, alter, update, or delete any object or row.

SELECT
    DB_NAME() AS [DatabaseName],
    OBJECT_ID(N'dbo.BlueprintSection', N'U') AS [BlueprintSectionObjectId],
    CASE WHEN COL_LENGTH(N'dbo.BlueprintSection', N'QuestionType') IS NOT NULL
         THEN 'PRESENT' ELSE 'MISSING' END AS [QuestionTypeColumn],
    CASE WHEN COL_LENGTH(N'dbo.BlueprintSection', N'ScoringRule') IS NOT NULL
         THEN 'PRESENT' ELSE 'MISSING' END AS [ScoringRuleColumn],
    CASE WHEN COL_LENGTH(N'dbo.BlueprintSection', N'PartCountPerQuestion') IS NOT NULL
         THEN 'PRESENT' ELSE 'MISSING' END AS [PartCountColumn];

SELECT
    name AS [ConstraintName],
    is_disabled AS [IsDisabled],
    is_not_trusted AS [IsNotTrusted],
    definition AS [Definition]
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID(N'dbo.BlueprintSection')
  AND name = N'CK_BlueprintSection_CompositePartMetadata';

SELECT
    QuestionType,
    ScoringRule,
    PartCountPerQuestion,
    COUNT_BIG(*) AS [RowCount]
FROM dbo.BlueprintSection
GROUP BY QuestionType, ScoringRule, PartCountPerQuestion
ORDER BY QuestionType, ScoringRule, PartCountPerQuestion;

SELECT
    CASE WHEN EXISTS (
        SELECT 1
        FROM dbo.BlueprintSection
        WHERE QuestionType IS NULL
           OR ScoringRule IS NULL
           OR (QuestionType = 'Composite'
               AND ScoringRule NOT IN ('TieredTrueFalse', 'WeightedParts'))
           OR (QuestionType <> 'Composite' AND ScoringRule <> 'AllOrNothing')
           OR (QuestionType <> 'Composite' AND PartCountPerQuestion IS NOT NULL)
           OR (PartCountPerQuestion IS NOT NULL AND PartCountPerQuestion <= 0))
         THEN 'BLOCKED_INVALID_POLICY'
         ELSE 'READY'
    END AS [PolicyStatus],
    (SELECT COUNT_BIG(*)
     FROM dbo.BlueprintSection
     WHERE QuestionType = 'Composite'
       AND ScoringRule IN ('TieredTrueFalse', 'WeightedParts')
       AND PartCountPerQuestion IS NOT NULL) AS [CompositeRowsToNormalize],
    (SELECT COUNT_BIG(*)
     FROM dbo.BlueprintSection
     WHERE QuestionType <> 'Composite'
       AND PartCountPerQuestion IS NOT NULL) AS [NonCompositeRowsWithPartCount];

SELECT
    N'Run 009 only after this read-only preflight. Execute the complete 009 script in one sqlcmd session with -b; do not rerun 001, 005, or 006 on the production database.'
    AS [ExecutionInstruction];
