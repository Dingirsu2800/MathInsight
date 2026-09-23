SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

SELECT
    DB_NAME() AS [DatabaseName],
    CASE WHEN COL_LENGTH(N'dbo.BlueprintDetail', N'QuestionType') IS NOT NULL
          AND COL_LENGTH(N'dbo.BlueprintDetail', N'ScoringRule') IS NOT NULL
          AND EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_BlueprintDetail_Section_Tag_Difficulty_Type_Rule' AND object_id = OBJECT_ID(N'dbo.BlueprintDetail'))
         THEN 'APPLIED' ELSE 'NOT_APPLIED' END AS [MigrationStatus];

IF OBJECT_ID(N'dbo.BlueprintSection', N'U') IS NULL
   OR OBJECT_ID(N'dbo.BlueprintDetail', N'U') IS NULL
   OR COL_LENGTH(N'dbo.BlueprintDetail', N'QuestionType') IS NULL
   OR COL_LENGTH(N'dbo.BlueprintDetail', N'ScoringRule') IS NULL
BEGIN
    SELECT 'SCHEMA_NOT_READY' AS [PolicyStatus];
    RETURN;
END;

EXEC(N'
SELECT
    SUM(CASE WHEN section.QuestionType = ''Composite'' AND section.PartCountPerQuestion IS NOT NULL THEN 1 ELSE 0 END) AS [CompositeRowsStillWithPartCount],
    SUM(CASE WHEN section.QuestionType = ''Mixed'' AND section.ScoringRule IS NOT NULL THEN 1 ELSE 0 END) AS [MixedSectionsWithSectionRule],
    SUM(CASE WHEN section.QuestionType NOT IN (''Composite'', ''Mixed'') AND section.PartCountPerQuestion IS NOT NULL THEN 1 ELSE 0 END) AS [NonCompositeRowsWithPartCount],
    SUM(CASE WHEN section.QuestionType IS NULL OR section.QuestionType NOT IN (''SingleChoice'', ''MultipleChoice'', ''TrueFalse'', ''ShortAnswer'', ''Composite'', ''Mixed'') THEN 1 ELSE 0 END) AS [InvalidSectionType]
FROM dbo.BlueprintSection AS section;

SELECT
    CASE WHEN EXISTS (
        SELECT 1
        FROM dbo.BlueprintDetail AS detail
        INNER JOIN dbo.BlueprintSection AS section
          ON section.BlueprintSectionID = detail.BlueprintSectionID
         AND section.BlueprintID = detail.BlueprintID
        WHERE (section.QuestionType = ''Mixed'' AND (detail.QuestionType IS NULL OR detail.ScoringRule IS NULL))
           OR (section.QuestionType <> ''Mixed'' AND (detail.QuestionType IS NOT NULL OR detail.ScoringRule IS NOT NULL))
           OR (detail.QuestionType = ''Composite'' AND detail.ScoringRule NOT IN (''TieredTrueFalse'', ''WeightedParts''))
           OR (detail.QuestionType IN (''SingleChoice'', ''MultipleChoice'', ''TrueFalse'', ''ShortAnswer'') AND detail.ScoringRule <> ''AllOrNothing'')
    ) THEN ''INVALID'' ELSE ''READY'' END AS [DetailPolicyStatus],
    CASE WHEN EXISTS (
        SELECT 1
        FROM dbo.BlueprintDetail AS detail
        INNER JOIN dbo.BlueprintSection AS section
          ON section.BlueprintSectionID = detail.BlueprintSectionID
         AND section.BlueprintID = detail.BlueprintID
        GROUP BY detail.BlueprintSectionID, detail.TagID, detail.DifficultyID,
                 CASE WHEN section.QuestionType = ''Mixed'' THEN detail.QuestionType ELSE section.QuestionType END,
                 CASE WHEN section.QuestionType = ''Mixed'' THEN detail.ScoringRule ELSE section.ScoringRule END
        HAVING COUNT_BIG(*) > 1
    ) THEN ''DUPLICATE'' ELSE ''UNIQUE'' END AS [EffectiveAllocationStatus];
');
