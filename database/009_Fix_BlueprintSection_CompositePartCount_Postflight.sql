SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- READ-ONLY POSTFLIGHT for 009_Fix_BlueprintSection_CompositePartCount.sql.

SELECT
    DB_NAME() AS [DatabaseName],
    CASE WHEN EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = N'CK_BlueprintSection_CompositePartMetadata'
          AND parent_object_id = OBJECT_ID(N'dbo.BlueprintSection')
          AND is_disabled = 0
          AND is_not_trusted = 0
          AND definition LIKE N'%PartCountPerQuestion%IS NULL%'
          AND definition NOT LIKE N'%PartCountPerQuestion%IS NOT NULL%'
          AND definition LIKE N'%TieredTrueFalse%'
          AND definition LIKE N'%WeightedParts%'
          AND definition LIKE N'%AllOrNothing%')
         THEN 'APPLIED'
         ELSE 'NOT_APPLIED'
    END AS [MigrationStatus];

SELECT
    QuestionType,
    ScoringRule,
    PartCountPerQuestion,
    COUNT_BIG(*) AS [RowCount]
FROM dbo.BlueprintSection
GROUP BY QuestionType, ScoringRule, PartCountPerQuestion
ORDER BY QuestionType, ScoringRule, PartCountPerQuestion;

SELECT
    (SELECT COUNT_BIG(*)
     FROM dbo.BlueprintSection
     WHERE QuestionType = 'Composite'
       AND ScoringRule IN ('TieredTrueFalse', 'WeightedParts')
       AND PartCountPerQuestion IS NOT NULL) AS [CompositeRowsStillWithPartCount],
    (SELECT COUNT_BIG(*)
     FROM dbo.BlueprintSection
     WHERE QuestionType <> 'Composite'
       AND PartCountPerQuestion IS NOT NULL) AS [NonCompositeRowsWithPartCount],
    (SELECT COUNT_BIG(*)
     FROM dbo.BlueprintSection
     WHERE QuestionType IS NULL
        OR ScoringRule IS NULL
        OR (QuestionType = 'Composite'
            AND ScoringRule NOT IN ('TieredTrueFalse', 'WeightedParts'))
        OR (QuestionType <> 'Composite' AND ScoringRule <> 'AllOrNothing')
        OR (QuestionType <> 'Composite' AND PartCountPerQuestion IS NOT NULL)
        OR (PartCountPerQuestion IS NOT NULL AND PartCountPerQuestion <= 0)) AS [InvalidPolicyRows];
