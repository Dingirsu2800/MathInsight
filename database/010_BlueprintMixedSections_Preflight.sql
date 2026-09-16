SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

SELECT
    DB_NAME() AS [DatabaseName],
    CASE WHEN OBJECT_ID(N'dbo.BlueprintSection', N'U') IS NOT NULL THEN 'PRESENT' ELSE 'MISSING' END AS [BlueprintSection],
    CASE WHEN OBJECT_ID(N'dbo.BlueprintDetail', N'U') IS NOT NULL THEN 'PRESENT' ELSE 'MISSING' END AS [BlueprintDetail],
    CASE WHEN COL_LENGTH(N'dbo.BlueprintSection', N'ScoringRule') IS NOT NULL THEN 'PRESENT' ELSE 'MISSING' END AS [SectionScoringRule],
    CASE WHEN COL_LENGTH(N'dbo.BlueprintDetail', N'QuestionType') IS NOT NULL THEN 'PRESENT' ELSE 'MISSING' END AS [DetailQuestionType],
    CASE WHEN COL_LENGTH(N'dbo.BlueprintDetail', N'ScoringRule') IS NOT NULL THEN 'PRESENT' ELSE 'MISSING' END AS [DetailScoringRule],
    CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_BlueprintDetail_Section_Tag_Difficulty_Type_Rule' AND object_id = OBJECT_ID(N'dbo.BlueprintDetail')) THEN 'PRESENT' ELSE 'MISSING' END AS [MixedAllocationIndex];

IF OBJECT_ID(N'dbo.BlueprintSection', N'U') IS NULL
   OR OBJECT_ID(N'dbo.BlueprintDetail', N'U') IS NULL
   OR COL_LENGTH(N'dbo.BlueprintSection', N'ScoringRule') IS NULL
   OR COL_LENGTH(N'dbo.BlueprintDetail', N'QuestionType') IS NULL
   OR COL_LENGTH(N'dbo.BlueprintDetail', N'ScoringRule') IS NULL
BEGIN
    SELECT 'BLOCKED_SCHEMA' AS [PolicyStatus], 'Apply the base schema and 005/006 before 010.' AS [Message];
    RETURN;
END;

EXEC(N'
SELECT QuestionType, ScoringRule, PartCountPerQuestion, COUNT_BIG(*) AS [RowCount]
FROM dbo.BlueprintSection
GROUP BY QuestionType, ScoringRule, PartCountPerQuestion
ORDER BY QuestionType, ScoringRule, PartCountPerQuestion;

SELECT
    SUM(CASE WHEN section.QuestionType = ''Composite'' AND section.PartCountPerQuestion IS NOT NULL THEN 1 ELSE 0 END) AS [CompositeRowsToNormalize],
    SUM(CASE WHEN section.QuestionType = ''Mixed'' THEN 1 ELSE 0 END) AS [ExistingMixedSections],
    SUM(CASE WHEN section.QuestionType NOT IN (''Composite'', ''Mixed'') AND section.PartCountPerQuestion IS NOT NULL THEN 1 ELSE 0 END) AS [InvalidNonCompositePartCount]
FROM dbo.BlueprintSection AS section;

SELECT
    CASE WHEN EXISTS (
        SELECT 1
        FROM dbo.BlueprintSection AS section
        LEFT JOIN dbo.BlueprintDetail AS detail
          ON detail.BlueprintSectionID = section.BlueprintSectionID
         AND detail.BlueprintID = section.BlueprintID
        WHERE section.QuestionType = ''Mixed''
          AND (detail.QuestionType IS NULL OR detail.ScoringRule IS NULL)
    ) THEN ''BLOCKED_MIXED_DETAIL_POLICY''
    WHEN EXISTS (
        SELECT 1
        FROM dbo.BlueprintDetail AS detail
        INNER JOIN dbo.BlueprintSection AS section
          ON section.BlueprintSectionID = detail.BlueprintSectionID
         AND section.BlueprintID = detail.BlueprintID
        GROUP BY detail.BlueprintSectionID, detail.TagID, detail.DifficultyID,
                 CASE WHEN section.QuestionType = ''Mixed'' THEN detail.QuestionType ELSE section.QuestionType END,
                 CASE WHEN section.QuestionType = ''Mixed'' THEN detail.ScoringRule ELSE section.ScoringRule END
        HAVING COUNT_BIG(*) > 1
    ) THEN ''BLOCKED_DUPLICATE_EFFECTIVE_ROW''
    ELSE ''READY'' END AS [PolicyStatus];

SELECT N''Run 010 only after this read-only preflight. Execute the complete script in one sqlcmd session with -b; do not rerun 005, 006, or 009 on the production database.'' AS [ExecutionInstruction];
');
