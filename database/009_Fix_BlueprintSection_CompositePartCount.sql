SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.BlueprintSection', N'U') IS NULL
        THROW 50010, '009 cannot continue: dbo.BlueprintSection is missing; apply the base schema first.', 1;

    IF COL_LENGTH(N'dbo.BlueprintSection', N'QuestionType') IS NULL
       OR COL_LENGTH(N'dbo.BlueprintSection', N'ScoringRule') IS NULL
       OR COL_LENGTH(N'dbo.BlueprintSection', N'PartCountPerQuestion') IS NULL
        THROW 50011, '009 cannot continue: BlueprintSection composite policy columns are missing.', 1;

    -- Validate every row before dropping the existing constraint. This keeps
    -- invalid policy data and the old schema unchanged when the migration
    -- must stop for DBA review.
    IF EXISTS (
        SELECT 1
        FROM dbo.BlueprintSection
        WHERE QuestionType IS NULL
           OR ScoringRule IS NULL
           OR (QuestionType = 'Composite'
               AND ScoringRule NOT IN ('TieredTrueFalse', 'WeightedParts'))
           OR (QuestionType <> 'Composite'
               AND ScoringRule <> 'AllOrNothing')
           OR (QuestionType <> 'Composite'
               AND PartCountPerQuestion IS NOT NULL)
           OR (PartCountPerQuestion IS NOT NULL AND PartCountPerQuestion <= 0))
        THROW 50012, '009 cannot continue: BlueprintSection contains an invalid composite policy.', 1;

    IF EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = N'CK_BlueprintSection_CompositePartMetadata'
          AND parent_object_id = OBJECT_ID(N'dbo.BlueprintSection'))
        ALTER TABLE dbo.BlueprintSection
            DROP CONSTRAINT CK_BlueprintSection_CompositePartMetadata;

    UPDATE dbo.BlueprintSection
    SET PartCountPerQuestion = NULL
    WHERE QuestionType = 'Composite'
      AND ScoringRule IN ('TieredTrueFalse', 'WeightedParts')
      AND PartCountPerQuestion IS NOT NULL;

    ALTER TABLE dbo.BlueprintSection WITH CHECK
        ADD CONSTRAINT CK_BlueprintSection_CompositePartMetadata CHECK (
            ([QuestionType] = 'Composite'
             AND [PartCountPerQuestion] IS NULL
             AND [ScoringRule] IN ('TieredTrueFalse', 'WeightedParts')) OR
            ([QuestionType] <> 'Composite'
             AND [PartCountPerQuestion] IS NULL
             AND [ScoringRule] = 'AllOrNothing'));

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
