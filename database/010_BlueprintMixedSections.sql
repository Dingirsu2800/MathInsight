SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.BlueprintSection', N'U') IS NULL
       OR OBJECT_ID(N'dbo.BlueprintDetail', N'U') IS NULL
        THROW 50010, '010 cannot continue: BlueprintSection or BlueprintDetail is missing; apply the base schema first.', 1;

    IF COL_LENGTH(N'dbo.BlueprintSection', N'QuestionType') IS NULL
       OR COL_LENGTH(N'dbo.BlueprintSection', N'ScoringRule') IS NULL
       OR COL_LENGTH(N'dbo.BlueprintSection', N'PartCountPerQuestion') IS NULL
        THROW 50011, '010 cannot continue: BlueprintSection policy columns are missing; apply 005/006 first.', 1;

    IF COL_LENGTH(N'dbo.BlueprintDetail', N'QuestionType') IS NULL
        EXEC(N'ALTER TABLE dbo.BlueprintDetail ADD QuestionType VARCHAR(30) NULL;');

    IF COL_LENGTH(N'dbo.BlueprintDetail', N'ScoringRule') IS NULL
        EXEC(N'ALTER TABLE dbo.BlueprintDetail ADD ScoringRule VARCHAR(30) NULL;');

    -- Mixed sections have no section-level grading rule. Keep the old
    -- homogeneous payloads intact and let their validated rows inherit it.
    EXEC(N'ALTER TABLE dbo.BlueprintSection ALTER COLUMN ScoringRule VARCHAR(30) NULL;');

    -- The new detail columns are added dynamically above. Keep all references
    -- to them in a dynamic batch so SQL Server does not compile the old batch
    -- before those columns exist.
    EXEC(N'
        IF EXISTS (
            SELECT 1
            FROM dbo.BlueprintSection
            WHERE QuestionType IS NULL
               OR QuestionType NOT IN (''SingleChoice'', ''MultipleChoice'', ''TrueFalse'', ''ShortAnswer'', ''Composite'', ''Mixed'')
               OR (QuestionType = ''Mixed'' AND (ScoringRule IS NOT NULL OR PartCountPerQuestion IS NOT NULL))
               OR (QuestionType = ''Composite'' AND (ScoringRule IS NULL OR ScoringRule NOT IN (''TieredTrueFalse'', ''WeightedParts'')))
               OR (QuestionType = ''Composite'' AND PartCountPerQuestion IS NOT NULL AND PartCountPerQuestion <= 0)
               OR (QuestionType NOT IN (''Composite'', ''Mixed'') AND (ScoringRule <> ''AllOrNothing'' OR PartCountPerQuestion IS NOT NULL))
               OR (QuestionType NOT IN (''Composite'', ''Mixed'') AND ScoringRule IS NULL))
            THROW 50012, ''010 cannot continue: BlueprintSection contains an invalid Mixed or homogeneous policy.'', 1;

        IF EXISTS (
            SELECT 1
            FROM dbo.BlueprintDetail AS detail
            INNER JOIN dbo.BlueprintSection AS section
                ON section.BlueprintSectionID = detail.BlueprintSectionID
               AND section.BlueprintID = detail.BlueprintID
            WHERE (detail.QuestionType IS NULL AND detail.ScoringRule IS NOT NULL)
               OR (detail.QuestionType IS NOT NULL AND detail.ScoringRule IS NULL)
               OR (detail.QuestionType IS NOT NULL AND detail.QuestionType NOT IN (''SingleChoice'', ''MultipleChoice'', ''TrueFalse'', ''ShortAnswer'', ''Composite''))
               OR (detail.QuestionType = ''Composite'' AND detail.ScoringRule NOT IN (''TieredTrueFalse'', ''WeightedParts''))
               OR (detail.QuestionType IN (''SingleChoice'', ''MultipleChoice'', ''TrueFalse'', ''ShortAnswer'') AND detail.ScoringRule <> ''AllOrNothing'')
               OR (section.QuestionType = ''Mixed'' AND (detail.QuestionType IS NULL OR detail.ScoringRule IS NULL))
               OR (section.QuestionType <> ''Mixed'' AND (detail.QuestionType IS NOT NULL OR detail.ScoringRule IS NOT NULL)))
            THROW 50013, ''010 cannot continue: BlueprintDetail contains an invalid or ambiguous effective policy.'', 1;

        IF EXISTS (
            SELECT 1
            FROM (
                SELECT
                    detail.BlueprintSectionID,
                    detail.TagID,
                    detail.DifficultyID,
                    CASE WHEN section.QuestionType = ''Mixed'' THEN detail.QuestionType ELSE section.QuestionType END AS EffectiveQuestionType,
                    CASE WHEN section.QuestionType = ''Mixed'' THEN detail.ScoringRule ELSE section.ScoringRule END AS EffectiveScoringRule
                FROM dbo.BlueprintDetail AS detail
                INNER JOIN dbo.BlueprintSection AS section
                    ON section.BlueprintSectionID = detail.BlueprintSectionID
                   AND section.BlueprintID = detail.BlueprintID
            ) AS effective
            GROUP BY BlueprintSectionID, TagID, DifficultyID, EffectiveQuestionType, EffectiveScoringRule
            HAVING COUNT_BIG(*) > 1)
            THROW 50014, ''010 cannot continue: duplicate effective BlueprintDetail allocation rows exist.'', 1;
    ');

    -- 009 may already have normalized these rows. This remains safe for a
    -- database upgraded directly from 006 and does not touch scoring history.
    UPDATE dbo.BlueprintSection
    SET PartCountPerQuestion = NULL
    WHERE QuestionType = 'Composite'
      AND ScoringRule IN ('TieredTrueFalse', 'WeightedParts')
      AND PartCountPerQuestion IS NOT NULL;

    IF EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = N'CK_BlueprintSection_QuestionType'
          AND parent_object_id = OBJECT_ID(N'dbo.BlueprintSection'))
        EXEC(N'ALTER TABLE dbo.BlueprintSection DROP CONSTRAINT CK_BlueprintSection_QuestionType;');

    IF EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = N'CK_BlueprintSection_ScoringRule'
          AND parent_object_id = OBJECT_ID(N'dbo.BlueprintSection'))
        EXEC(N'ALTER TABLE dbo.BlueprintSection DROP CONSTRAINT CK_BlueprintSection_ScoringRule;');

    IF EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = N'CK_BlueprintSection_CompositePartMetadata'
          AND parent_object_id = OBJECT_ID(N'dbo.BlueprintSection'))
        EXEC(N'ALTER TABLE dbo.BlueprintSection DROP CONSTRAINT CK_BlueprintSection_CompositePartMetadata;');

    IF EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = N'CK_BlueprintDetail_QuestionType'
          AND parent_object_id = OBJECT_ID(N'dbo.BlueprintDetail'))
        EXEC(N'ALTER TABLE dbo.BlueprintDetail DROP CONSTRAINT CK_BlueprintDetail_QuestionType;');

    IF EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = N'CK_BlueprintDetail_ScoringRule'
          AND parent_object_id = OBJECT_ID(N'dbo.BlueprintDetail'))
        EXEC(N'ALTER TABLE dbo.BlueprintDetail DROP CONSTRAINT CK_BlueprintDetail_ScoringRule;');

    IF EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = N'CK_BlueprintDetail_Policy'
          AND parent_object_id = OBJECT_ID(N'dbo.BlueprintDetail'))
        EXEC(N'ALTER TABLE dbo.BlueprintDetail DROP CONSTRAINT CK_BlueprintDetail_Policy;');

    EXEC(N'ALTER TABLE dbo.BlueprintSection WITH CHECK ADD CONSTRAINT CK_BlueprintSection_QuestionType CHECK ([QuestionType] IN (''SingleChoice'', ''MultipleChoice'', ''TrueFalse'', ''ShortAnswer'', ''Composite'', ''Mixed''));');
    EXEC(N'ALTER TABLE dbo.BlueprintSection WITH CHECK ADD CONSTRAINT CK_BlueprintSection_ScoringRule CHECK ([ScoringRule] IS NULL OR [ScoringRule] IN (''AllOrNothing'', ''TieredTrueFalse'', ''WeightedParts''));');
    EXEC(N'ALTER TABLE dbo.BlueprintSection WITH CHECK ADD CONSTRAINT CK_BlueprintSection_CompositePartMetadata CHECK ((([QuestionType] = ''Mixed'' AND [PartCountPerQuestion] IS NULL AND [ScoringRule] IS NULL) OR ([QuestionType] = ''Composite'' AND [PartCountPerQuestion] IS NULL AND [ScoringRule] IN (''TieredTrueFalse'', ''WeightedParts'')) OR ([QuestionType] IN (''SingleChoice'', ''MultipleChoice'', ''TrueFalse'', ''ShortAnswer'') AND [PartCountPerQuestion] IS NULL AND [ScoringRule] = ''AllOrNothing'')));');
    EXEC(N'ALTER TABLE dbo.BlueprintDetail WITH CHECK ADD CONSTRAINT CK_BlueprintDetail_QuestionType CHECK ([QuestionType] IS NULL OR [QuestionType] IN (''SingleChoice'', ''MultipleChoice'', ''TrueFalse'', ''ShortAnswer'', ''Composite''));');
    EXEC(N'ALTER TABLE dbo.BlueprintDetail WITH CHECK ADD CONSTRAINT CK_BlueprintDetail_ScoringRule CHECK ([ScoringRule] IS NULL OR [ScoringRule] IN (''AllOrNothing'', ''TieredTrueFalse'', ''WeightedParts''));');
    EXEC(N'ALTER TABLE dbo.BlueprintDetail WITH CHECK ADD CONSTRAINT CK_BlueprintDetail_Policy CHECK (([QuestionType] IS NULL AND [ScoringRule] IS NULL) OR ([QuestionType] IN (''SingleChoice'', ''MultipleChoice'', ''TrueFalse'', ''ShortAnswer'') AND [ScoringRule] = ''AllOrNothing'') OR ([QuestionType] = ''Composite'' AND [ScoringRule] IN (''TieredTrueFalse'', ''WeightedParts'')));');

    IF EXISTS (
        SELECT 1
        FROM sys.key_constraints
        WHERE name = N'UQ_BlueprintDetail_Section_Tag_Difficulty'
          AND parent_object_id = OBJECT_ID(N'dbo.BlueprintDetail'))
        EXEC(N'ALTER TABLE dbo.BlueprintDetail DROP CONSTRAINT UQ_BlueprintDetail_Section_Tag_Difficulty;');
    ELSE IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'UQ_BlueprintDetail_Section_Tag_Difficulty'
          AND object_id = OBJECT_ID(N'dbo.BlueprintDetail'))
        EXEC(N'DROP INDEX UQ_BlueprintDetail_Section_Tag_Difficulty ON dbo.BlueprintDetail;');

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'UQ_BlueprintDetail_Section_Tag_Difficulty_Type_Rule'
          AND object_id = OBJECT_ID(N'dbo.BlueprintDetail'))
        EXEC(N'CREATE UNIQUE INDEX UQ_BlueprintDetail_Section_Tag_Difficulty_Type_Rule ON dbo.BlueprintDetail (BlueprintSectionID, TagID, DifficultyID, QuestionType, ScoringRule);');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
