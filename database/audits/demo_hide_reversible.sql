-- Reversible presentation cleanup. Does not delete reports, sessions, activity, or achievements.
-- Archived tests remain in Student history; inactive questions remain in the Expert list.
-- Run as a single batch against the database named backup.
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Action varchar(10) = 'Preview'; -- Preview | Hide | Restore
DECLARE @Commit bit = 0;                 -- 0: rollback; 1: commit Hide/Restore
DECLARE @ExpectedDatabaseName sysname = N'backup';

IF DB_NAME() <> @ExpectedDatabaseName
    THROW 51100, 'Wrong database. No changes made.', 1;
IF @@TRANCOUNT <> 0
    THROW 51101, 'Run outside an existing transaction.', 1;
IF @Action NOT IN ('Preview', 'Hide', 'Restore') OR (@Commit = 1 AND @Action = 'Preview')
    THROW 51102, 'Choose Preview, Hide, or Restore; Preview cannot commit.', 1;

DECLARE @Tags table (TagID varchar(36) PRIMARY KEY, OriginalIsActive bit NOT NULL);
INSERT INTO @Tags VALUES
    ('4d5b7fe0-f0b3-49f8-a658-86b74efc7e6e', 1),
    ('705109d1-5d77-4b82-823d-fd41c71418f7', 1),
    ('de4818df-1906-45b9-a4ec-8af002807671', 1),
    ('bcde90ee-85fe-4054-974d-e2e08bc21d75', 1),
    ('89cf7e58-a63e-40e3-9706-ab54eac24873', 1),
    ('da0b7bac-2dfe-47d8-8766-31b290577024', 1),
    ('3c33fb39-c2ea-43e2-8725-67de22a4bcb9', 1),
    ('8421645b-7cd6-460b-a0a6-1d669ff87921', 0);

DECLARE @Questions table (QuestionID varchar(36) PRIMARY KEY);
INSERT INTO @Questions VALUES
    ('0879b589-d627-4d1b-8efd-f9efd100ee1e'),
    ('23c659eb-1e73-48c2-8f94-be2dd5416be7'),
    ('459eed6e-6dbd-49fe-939e-6b09f72e89b2'),
    ('4834e3b9-250d-430b-987a-60c548ad35f4'),
    ('90e0c88f-8c0c-48fc-926d-ad82e25c28c1'),
    ('aff97072-4633-4c8a-a02f-8eed06a6746f'),
    ('b46d1f91-fdd3-42f5-9496-2bf8f60da04f'),
    ('bfa12aaf-dd24-4020-b647-31fd2d72167a'),
    ('ce2a5d7e-6d39-42ab-8145-cee08e53eb4f'),
    ('e57c6823-74f4-494b-bf75-789f431238ef'),
    ('f7d70d22-6d58-4f5f-9552-6fe38dfab9ce');

DECLARE @Blueprints table (BlueprintID varchar(36) PRIMARY KEY, OriginalStatus varchar(20) NOT NULL);
INSERT INTO @Blueprints VALUES
    ('0530822b-3faa-4210-9e6e-3e5656ac7d86', 'Active'),
    ('1d721209-f1d8-4db7-bd43-448f64f7892c', 'Approved'),
    ('bb9d7543-1a26-42bd-ae82-48deec731532', 'Approved'),
    ('d11ba371-5bea-4f97-963e-10143999328d', 'Active'),
    ('dfd0ebd4-3e33-4ba9-a13e-948907eb3a9e', 'Active');

DECLARE @Tests table (TestID varchar(36) PRIMARY KEY);
INSERT INTO @Tests VALUES
    ('5007e523-e477-4872-b095-be0827235be7'),
    ('7d121885-4d84-44f8-812c-df1b331fc4bb'),
    ('908dc5fe-0e85-43e6-98c8-ff8aadc67091');

BEGIN TRY
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
    BEGIN TRANSACTION;

    IF (SELECT COUNT(*) FROM dbo.TagTopic t JOIN @Tags x ON x.TagID = t.TagID) <> 8
        OR (SELECT COUNT(*) FROM dbo.Question q JOIN @Questions x ON x.QuestionID = q.QuestionID) <> 11
        OR (SELECT COUNT(*) FROM dbo.Blueprint b JOIN @Blueprints x ON x.BlueprintID = b.BlueprintID) <> 5
        OR (SELECT COUNT(*) FROM dbo.Test t JOIN @Tests x ON x.TestID = t.TestID) <> 3
        THROW 51103, 'Approved entity set changed. Stop and rerun the inventory.', 1;

    IF EXISTS (SELECT 1 FROM dbo.TagTopic child JOIN @Tags parent ON parent.TagID = child.ParentTagID
               LEFT JOIN @Tags allowed ON allowed.TagID = child.TagID WHERE allowed.TagID IS NULL)
        OR EXISTS (SELECT 1 FROM dbo.QuestionTopic qt JOIN @Tags t ON t.TagID = qt.TagID
                   LEFT JOIN @Questions q ON q.QuestionID = qt.QuestionID WHERE q.QuestionID IS NULL)
        OR EXISTS (SELECT 1 FROM dbo.QuestionTopic qt JOIN @Questions q ON q.QuestionID = qt.QuestionID
                   LEFT JOIN @Tags t ON t.TagID = qt.TagID WHERE t.TagID IS NULL)
        OR EXISTS (SELECT 1 FROM dbo.BlueprintDetail d JOIN @Tags t ON t.TagID = d.TagID
                   LEFT JOIN @Blueprints b ON b.BlueprintID = d.BlueprintID WHERE b.BlueprintID IS NULL)
        OR EXISTS (SELECT 1 FROM dbo.BlueprintDetail d JOIN @Blueprints b ON b.BlueprintID = d.BlueprintID
                   LEFT JOIN @Tags t ON t.TagID = d.TagID WHERE t.TagID IS NULL)
        OR EXISTS (SELECT 1 FROM dbo.Test t JOIN @Blueprints b ON b.BlueprintID = t.BlueprintID
                   LEFT JOIN @Tests allowed ON allowed.TestID = t.TestID WHERE allowed.TestID IS NULL)
        OR EXISTS (SELECT 1 FROM dbo.TestQuestion tq JOIN @Questions q ON q.QuestionID = tq.QuestionID
                   LEFT JOIN @Tests t ON t.TestID = tq.TestID WHERE t.TestID IS NULL)
        OR EXISTS (SELECT 1 FROM dbo.TestQuestion tq JOIN @Tags x ON x.TagID = tq.RecommendedForTagID
                   LEFT JOIN @Tests t ON t.TestID = tq.TestID WHERE t.TestID IS NULL)
        OR EXISTS (SELECT 1 FROM dbo.TestQuestion tq JOIN @Tests t ON t.TestID = tq.TestID
                   LEFT JOIN @Questions q ON q.QuestionID = tq.QuestionID WHERE q.QuestionID IS NULL)
        OR EXISTS (SELECT 1 FROM dbo.Lecture l JOIN @Tags x ON x.TagID = l.TagID)
        OR EXISTS (SELECT 1 FROM dbo.TestSession s JOIN @Tests t ON t.TestID = s.TestID
                   WHERE s.Status = 'InProgress')
        THROW 51104, 'New linked data or active session needs review before changing visibility.', 1;

    IF @Action = 'Hide' AND (
        EXISTS (SELECT 1 FROM dbo.TagTopic t JOIN @Tags x ON x.TagID = t.TagID
                WHERE t.IsActive <> x.OriginalIsActive)
        OR EXISTS (SELECT 1 FROM dbo.Question q JOIN @Questions x ON x.QuestionID = q.QuestionID
                   WHERE q.IsActive <> 1)
        OR EXISTS (SELECT 1 FROM dbo.Blueprint b JOIN @Blueprints x ON x.BlueprintID = b.BlueprintID
                   WHERE b.Status <> x.OriginalStatus)
        OR EXISTS (SELECT 1 FROM dbo.Test t JOIN @Tests x ON x.TestID = t.TestID
                   WHERE t.TestStatus <> 'Active')
    )
        THROW 51105, 'Current states differ from the audited original values.', 1;

    IF @Action = 'Restore' AND (
        EXISTS (SELECT 1 FROM dbo.TagTopic t JOIN @Tags x ON x.TagID = t.TagID WHERE t.IsActive <> 0)
        OR EXISTS (SELECT 1 FROM dbo.Question q JOIN @Questions x ON x.QuestionID = q.QuestionID WHERE q.IsActive <> 0)
        OR EXISTS (SELECT 1 FROM dbo.Blueprint b JOIN @Blueprints x ON x.BlueprintID = b.BlueprintID
                   WHERE b.Status <> 'Deactivated')
        OR EXISTS (SELECT 1 FROM dbo.Test t JOIN @Tests x ON x.TestID = t.TestID
                   WHERE t.TestStatus <> 'Archived')
    )
        THROW 51106, 'Some records changed after hiding; do not overwrite them blindly.', 1;

    SELECT DB_NAME() AS DatabaseName, @Action AS RequestedAction, @Commit AS CommitRequested,
        (SELECT COUNT(*) FROM @Tags) AS Topics,
        (SELECT COUNT(*) FROM @Questions) AS Questions,
        (SELECT COUNT(*) FROM @Blueprints) AS Blueprints,
        (SELECT COUNT(*) FROM @Tests) AS Tests,
        (SELECT COUNT(*) FROM dbo.TestSession s JOIN @Tests t ON t.TestID = s.TestID) AS PreservedSessions;

    IF @Action = 'Hide'
    BEGIN
        UPDATE t SET t.TestStatus = 'Archived' FROM dbo.Test t JOIN @Tests x ON x.TestID = t.TestID;
        UPDATE b SET b.Status = 'Deactivated' FROM dbo.Blueprint b JOIN @Blueprints x ON x.BlueprintID = b.BlueprintID;
        UPDATE q SET q.IsActive = 0 FROM dbo.Question q JOIN @Questions x ON x.QuestionID = q.QuestionID;
        UPDATE t SET t.IsActive = 0 FROM dbo.TagTopic t JOIN @Tags x ON x.TagID = t.TagID;
    END;

    IF @Action = 'Restore'
    BEGIN
        UPDATE t SET t.IsActive = x.OriginalIsActive FROM dbo.TagTopic t JOIN @Tags x ON x.TagID = t.TagID;
        UPDATE q SET q.IsActive = 1 FROM dbo.Question q JOIN @Questions x ON x.QuestionID = q.QuestionID;
        UPDATE b SET b.Status = x.OriginalStatus FROM dbo.Blueprint b JOIN @Blueprints x ON x.BlueprintID = b.BlueprintID;
        UPDATE t SET t.TestStatus = 'Active' FROM dbo.Test t JOIN @Tests x ON x.TestID = t.TestID;
    END;

    IF @Action = 'Hide' AND (
        EXISTS (SELECT 1 FROM dbo.TagTopic t JOIN @Tags x ON x.TagID = t.TagID WHERE t.IsActive <> 0)
        OR EXISTS (SELECT 1 FROM dbo.Question q JOIN @Questions x ON x.QuestionID = q.QuestionID WHERE q.IsActive <> 0)
        OR EXISTS (SELECT 1 FROM dbo.Blueprint b JOIN @Blueprints x ON x.BlueprintID = b.BlueprintID
                   WHERE b.Status <> 'Deactivated')
        OR EXISTS (SELECT 1 FROM dbo.Test t JOIN @Tests x ON x.TestID = t.TestID WHERE t.TestStatus <> 'Archived')
    )
        THROW 51107, 'Hide verification failed; transaction will roll back.', 1;

    IF @Action = 'Restore' AND (
        EXISTS (SELECT 1 FROM dbo.TagTopic t JOIN @Tags x ON x.TagID = t.TagID
                WHERE t.IsActive <> x.OriginalIsActive)
        OR EXISTS (SELECT 1 FROM dbo.Question q JOIN @Questions x ON x.QuestionID = q.QuestionID WHERE q.IsActive <> 1)
        OR EXISTS (SELECT 1 FROM dbo.Blueprint b JOIN @Blueprints x ON x.BlueprintID = b.BlueprintID
                   WHERE b.Status <> x.OriginalStatus)
        OR EXISTS (SELECT 1 FROM dbo.Test t JOIN @Tests x ON x.TestID = t.TestID WHERE t.TestStatus <> 'Active')
    )
        THROW 51108, 'Restore verification failed; transaction will roll back.', 1;

    IF @Commit = 1
    BEGIN
        COMMIT TRANSACTION;
        SELECT @Action AS Outcome, N'COMMITTED' AS TransactionState;
    END
    ELSE
    BEGIN
        ROLLBACK TRANSACTION;
        SELECT @Action AS Outcome, N'ROLLED_BACK' AS TransactionState;
    END;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
    THROW;
END CATCH;
