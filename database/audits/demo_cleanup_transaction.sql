-- One-time cleanup of the eight explicitly approved demo topics.
-- DO NOT COMMIT this hard-delete script without a verified database restore path.
-- Use demo_hide_reversible.sql while restore is unavailable.
-- StudyStreak and StudentBadge are intentionally preserved: they cannot be attributed
-- reliably to these five sessions and may include legitimate achievements.
-- Run against Azure SQL during a maintenance window, after confirming a restore point.
-- Defaults to preview only. The caller must opt in to a rollback rehearsal or commit.
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @RunDeletes bit = 0;       -- 0: preview; 1: execute inside a transaction
DECLARE @Commit bit = 0;           -- 0: rollback; 1: commit (requires both fields below)
DECLARE @BackupConfirmed bit = 0;  -- set only after checking Azure SQL restore availability
DECLARE @ExpectedDatabaseName sysname = N'';

IF @@TRANCOUNT <> 0
    THROW 51000, 'Run this script outside an existing transaction.', 1;
IF @Commit = 1 AND (@RunDeletes = 0 OR @BackupConfirmed = 0
                     OR @ExpectedDatabaseName = N'' OR DB_NAME() <> @ExpectedDatabaseName)
    THROW 51001, 'Commit requires delete mode, a confirmed restore point, and the exact database name.', 1;

DECLARE @Tags table (TagID varchar(36) PRIMARY KEY);
INSERT INTO @Tags (TagID) VALUES
    ('4d5b7fe0-f0b3-49f8-a658-86b74efc7e6e'),
    ('705109d1-5d77-4b82-823d-fd41c71418f7'),
    ('de4818df-1906-45b9-a4ec-8af002807671'),
    ('bcde90ee-85fe-4054-974d-e2e08bc21d75'),
    ('89cf7e58-a63e-40e3-9706-ab54eac24873'),
    ('da0b7bac-2dfe-47d8-8766-31b290577024'),
    ('3c33fb39-c2ea-43e2-8725-67de22a4bcb9'),
    ('8421645b-7cd6-460b-a0a6-1d669ff87921');

DECLARE @Questions table (QuestionID varchar(36) PRIMARY KEY);
INSERT INTO @Questions (QuestionID) VALUES
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

DECLARE @Blueprints table (BlueprintID varchar(36) PRIMARY KEY);
INSERT INTO @Blueprints (BlueprintID) VALUES
    ('0530822b-3faa-4210-9e6e-3e5656ac7d86'),
    ('1d721209-f1d8-4db7-bd43-448f64f7892c'),
    ('bb9d7543-1a26-42bd-ae82-48deec731532'),
    ('d11ba371-5bea-4f97-963e-10143999328d'),
    ('dfd0ebd4-3e33-4ba9-a13e-948907eb3a9e');

DECLARE @Tests table (TestID varchar(36) PRIMARY KEY);
INSERT INTO @Tests (TestID) VALUES
    ('5007e523-e477-4872-b095-be0827235be7'),
    ('7d121885-4d84-44f8-812c-df1b331fc4bb'),
    ('908dc5fe-0e85-43e6-98c8-ff8aadc67091');

BEGIN TRY
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
    BEGIN TRANSACTION;

    DECLARE @Sessions table (SessionID varchar(36) PRIMARY KEY);
    INSERT INTO @Sessions (SessionID)
    SELECT s.SessionID FROM dbo.TestSession s JOIN @Tests t ON t.TestID = s.TestID;

    DECLARE @Reports table (ReportID varchar(36) PRIMARY KEY);
    INSERT INTO @Reports (ReportID)
    SELECT r.ReportID FROM dbo.QuestionReport r JOIN @Questions q ON q.QuestionID = r.QuestionID;

    DECLARE @AffectedStudents table (StudentID varchar(36), Grade int,
                                     PRIMARY KEY (StudentID, Grade));
    INSERT INTO @AffectedStudents (StudentID, Grade)
    SELECT DISTINCT tm.StudentID, t.Grade
    FROM dbo.TagsMastery tm
    JOIN dbo.TagTopic t ON t.TagID = tm.TagID
    JOIN @Tags x ON x.TagID = tm.TagID;

    DECLARE @EntityIds table (EntityID varchar(36) PRIMARY KEY);
    INSERT INTO @EntityIds (EntityID)
    SELECT TagID FROM @Tags UNION SELECT QuestionID FROM @Questions
    UNION SELECT BlueprintID FROM @Blueprints UNION SELECT TestID FROM @Tests
    UNION SELECT SessionID FROM @Sessions UNION SELECT ReportID FROM @Reports;

    DECLARE @LinkedNotifications table (NotificationID varchar(36) PRIMARY KEY);
    INSERT INTO @LinkedNotifications (NotificationID)
    SELECT DISTINCT n.NotificationID FROM dbo.Notification n
    JOIN @EntityIds e ON CHARINDEX(e.EntityID, COALESCE(n.Link, '')) > 0
                     OR CHARINDEX(e.EntityID, COALESCE(n.DeduplicationKey, '')) > 0;

    IF (SELECT COUNT(*) FROM dbo.TagTopic t JOIN @Tags x ON x.TagID = t.TagID) <> 8
        OR (SELECT COUNT(*) FROM dbo.Question q JOIN @Questions x ON x.QuestionID = q.QuestionID) <> 11
        OR (SELECT COUNT(*) FROM dbo.Blueprint b JOIN @Blueprints x ON x.BlueprintID = b.BlueprintID) <> 5
        OR (SELECT COUNT(*) FROM dbo.Test t JOIN @Tests x ON x.TestID = t.TestID) <> 3
        OR (SELECT COUNT(*) FROM @Sessions) <> 5
        OR (SELECT COUNT(*) FROM @Reports) <> 12
        OR (SELECT COUNT(*) FROM @AffectedStudents) <> 5
        OR (SELECT COUNT(*) FROM dbo.TagsMastery tm JOIN @Tags x ON x.TagID = tm.TagID) <> 5
        OR (SELECT COUNT(*) FROM dbo.StudentTopicSessionResult r JOIN @Tags x ON x.TagID = r.TagID) <> 5
        OR (SELECT COUNT(*) FROM dbo.ActivityLog a JOIN @Sessions s ON s.SessionID = a.TestSessionID) <> 5
        OR (SELECT COUNT(*) FROM @LinkedNotifications) <> 27
        THROW 51002, 'Approved inventory changed; stop and rerun the read-only audit.', 1;

    IF EXISTS (SELECT 1 FROM dbo.TestSession s JOIN @Sessions x ON x.SessionID = s.SessionID
               WHERE s.Status = 'InProgress')
        THROW 51003, 'A selected test has an in-progress session.', 1;
    IF EXISTS (SELECT 1 FROM dbo.ActivityLog a JOIN dbo.TestSession s ON s.SessionID = a.TestSessionID
               JOIN @Sessions x ON x.SessionID = s.SessionID WHERE a.StudentID <> s.StudentID)
        THROW 51017, 'A session activity log belongs to a different student.', 1;
    IF EXISTS (SELECT 1 FROM dbo.TagTopic t JOIN @Tags x ON x.TagID = t.ParentTagID
               LEFT JOIN @Tags child ON child.TagID = t.TagID WHERE child.TagID IS NULL)
        THROW 51004, 'A selected parent topic has a child outside the approved set.', 1;
    IF EXISTS (SELECT 1 FROM dbo.QuestionTopic qt JOIN @Tags x ON x.TagID = qt.TagID
               LEFT JOIN @Questions q ON q.QuestionID = qt.QuestionID WHERE q.QuestionID IS NULL)
        OR EXISTS (SELECT 1 FROM dbo.QuestionTopic qt JOIN @Questions q ON q.QuestionID = qt.QuestionID
                   LEFT JOIN @Tags x ON x.TagID = qt.TagID WHERE x.TagID IS NULL)
        THROW 51005, 'Question-topic links no longer match the approved set.', 1;
    IF EXISTS (SELECT 1 FROM dbo.BlueprintDetail d JOIN @Tags x ON x.TagID = d.TagID
               LEFT JOIN @Blueprints b ON b.BlueprintID = d.BlueprintID WHERE b.BlueprintID IS NULL)
        OR EXISTS (SELECT 1 FROM dbo.BlueprintDetail d JOIN @Blueprints b ON b.BlueprintID = d.BlueprintID
                   LEFT JOIN @Tags x ON x.TagID = d.TagID WHERE x.TagID IS NULL)
        THROW 51006, 'Blueprint-topic links no longer match the approved set.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Test t JOIN @Blueprints b ON b.BlueprintID = t.BlueprintID
               LEFT JOIN @Tests x ON x.TestID = t.TestID WHERE x.TestID IS NULL)
        OR EXISTS (SELECT 1 FROM dbo.Test t JOIN @Tests x ON x.TestID = t.TestID
                   LEFT JOIN @Blueprints b ON b.BlueprintID = t.BlueprintID WHERE b.BlueprintID IS NULL)
        THROW 51007, 'Blueprint-test links no longer match the approved set.', 1;
    IF EXISTS (SELECT 1 FROM dbo.TestQuestion tq JOIN @Questions q ON q.QuestionID = tq.QuestionID
               LEFT JOIN @Tests t ON t.TestID = tq.TestID WHERE t.TestID IS NULL)
        OR EXISTS (SELECT 1 FROM dbo.TestQuestion tq JOIN @Tests t ON t.TestID = tq.TestID
                   LEFT JOIN @Questions q ON q.QuestionID = tq.QuestionID WHERE q.QuestionID IS NULL)
        OR EXISTS (SELECT 1 FROM dbo.TestQuestion tq JOIN @Tags x ON x.TagID = tq.RecommendedForTagID
                   LEFT JOIN @Tests t ON t.TestID = tq.TestID WHERE t.TestID IS NULL)
        OR EXISTS (SELECT 1 FROM dbo.TestQuestion tq
                   JOIN dbo.BlueprintDetail d ON d.BlueprintDetailID = tq.SourceBlueprintDetailID
                   JOIN @Blueprints b ON b.BlueprintID = d.BlueprintID
                   LEFT JOIN @Tests t ON t.TestID = tq.TestID WHERE t.TestID IS NULL)
        THROW 51008, 'A selected question, topic or blueprint detail is used by another test.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Lecture l JOIN @Tags x ON x.TagID = l.TagID)
        THROW 51009, 'A selected topic now has a lecture; review it separately.', 1;
    IF EXISTS (SELECT 1 FROM dbo.QuestionReport r JOIN @Sessions s ON s.SessionID = r.SessionID
               LEFT JOIN @Questions q ON q.QuestionID = r.QuestionID WHERE q.QuestionID IS NULL)
        OR EXISTS (SELECT 1 FROM dbo.QuestionReport r JOIN @Reports x ON x.ReportID = r.ReportID
                   LEFT JOIN @Sessions s ON s.SessionID = r.SessionID
                   WHERE r.SessionID IS NOT NULL AND s.SessionID IS NULL)
        THROW 51010, 'Report-session links reach data outside the approved set.', 1;
    IF EXISTS (SELECT 1 FROM dbo.TestQuestion tq JOIN @Reports r ON r.ReportID = tq.InvalidatedByReportID
               LEFT JOIN @Tests t ON t.TestID = tq.TestID WHERE t.TestID IS NULL)
        OR EXISTS (SELECT 1 FROM dbo.ScoreAdjustmentWork w JOIN @Reports r ON r.ReportID = w.ReportID)
        OR EXISTS (SELECT 1 FROM dbo.ScoreAdjustmentWork w JOIN @Sessions s ON s.SessionID = w.SessionID)
        THROW 51011, 'Score adjustment or invalidation references need separate review.', 1;
    IF EXISTS (SELECT 1 FROM dbo.QuestionReportIncident i JOIN @Questions q ON q.QuestionID = i.QuestionID
               JOIN dbo.QuestionReport r ON r.IncidentID = i.IncidentID
               LEFT JOIN @Reports x ON x.ReportID = r.ReportID WHERE x.ReportID IS NULL)
        THROW 51012, 'A candidate incident is shared with an out-of-scope report.', 1;
    IF EXISTS (SELECT 1 FROM dbo.StudentTopicSessionResult r JOIN @Tags x ON x.TagID = r.TagID
               LEFT JOIN @Sessions s ON s.SessionID = r.SessionID WHERE s.SessionID IS NULL)
        OR EXISTS (SELECT 1 FROM dbo.StudentTopicSessionResult r JOIN @Sessions s ON s.SessionID = r.SessionID
                   LEFT JOIN @Tags x ON x.TagID = r.TagID WHERE x.TagID IS NULL)
        OR EXISTS (SELECT 1 FROM dbo.TestAnswer a JOIN @Questions q ON q.QuestionID = a.QuestionID
                   LEFT JOIN @Sessions s ON s.SessionID = a.SessionID WHERE s.SessionID IS NULL)
        OR EXISTS (SELECT 1 FROM dbo.TestAnswer a JOIN @Sessions s ON s.SessionID = a.SessionID
                   LEFT JOIN @Questions q ON q.QuestionID = a.QuestionID WHERE q.QuestionID IS NULL)
        THROW 51013, 'Student result or answer links reach data outside the approved set.', 1;
    IF EXISTS (
        SELECT 1 FROM @AffectedStudents a WHERE NOT EXISTS (
            SELECT 1 FROM dbo.TagsMastery tm
            JOIN dbo.TagTopic t ON t.TagID = tm.TagID
            JOIN dbo.TagTopic p ON p.TagID = t.ParentTagID
            LEFT JOIN @Tags x ON x.TagID = t.TagID
            WHERE tm.StudentID = a.StudentID AND t.Grade = a.Grade AND p.Grade = a.Grade
              AND t.IsActive = 1 AND p.IsActive = 1 AND p.ParentTagID IS NULL AND x.TagID IS NULL
        )
    )
        THROW 51014, 'An affected student has no remaining mastery for competency recalculation.', 1;

    SELECT DB_NAME() AS DatabaseName,
        (SELECT COUNT(*) FROM @Tags) AS Topics,
        (SELECT COUNT(*) FROM @Questions) AS Questions,
        (SELECT COUNT(*) FROM @Blueprints) AS Blueprints,
        (SELECT COUNT(*) FROM @Tests) AS Tests,
        (SELECT COUNT(*) FROM @Sessions) AS Sessions,
        (SELECT COUNT(*) FROM @Reports) AS Reports,
        (SELECT COUNT(*) FROM @AffectedStudents) AS StudentsToRecalculate,
        (SELECT COUNT(*) FROM dbo.ActivityLog a JOIN @Sessions s ON s.SessionID = a.TestSessionID) AS SessionActivityLogs,
        (SELECT COUNT(*) FROM @LinkedNotifications) AS LinkedNotifications,
        @RunDeletes AS RunDeletes, @Commit AS CommitRequested;

    IF @RunDeletes = 1
    BEGIN
        DELETE n FROM dbo.Notification n
        JOIN @LinkedNotifications x ON x.NotificationID = n.NotificationID;
        DELETE a FROM dbo.ActivityLog a JOIN @Sessions s ON s.SessionID = a.TestSessionID;
        DELETE w FROM dbo.ScoreAdjustmentWork w JOIN @Reports r ON r.ReportID = w.ReportID;
        DELETE w FROM dbo.ScoreAdjustmentWork w JOIN @Sessions s ON s.SessionID = w.SessionID;
        DELETE tq FROM dbo.TestQuestion tq JOIN @Tests t ON t.TestID = tq.TestID;
        DELETE a FROM dbo.TestAnswer a JOIN @Sessions s ON s.SessionID = a.SessionID;
        DELETE i FROM dbo.TestIncidents i JOIN @Sessions s ON s.SessionID = i.SessionID;
        DELETE r FROM dbo.StudentTopicSessionResult r JOIN @Sessions s ON s.SessionID = r.SessionID;
        DELETE r FROM dbo.StudentTopicSessionResult r JOIN @Tags t ON t.TagID = r.TagID;
        DELETE r FROM dbo.QuestionReport r JOIN @Reports x ON x.ReportID = r.ReportID;
        DELETE i FROM dbo.QuestionReportIncident i JOIN @Questions q ON q.QuestionID = i.QuestionID;
        DELETE s FROM dbo.TestSession s JOIN @Sessions x ON x.SessionID = s.SessionID;
        DELETE t FROM dbo.Test t JOIN @Tests x ON x.TestID = t.TestID;
        DELETE b FROM dbo.Blueprint b JOIN @Blueprints x ON x.BlueprintID = b.BlueprintID;
        DELETE q FROM dbo.Question q JOIN @Questions x ON x.QuestionID = q.QuestionID;
        DELETE tm FROM dbo.TagsMastery tm JOIN @Tags x ON x.TagID = tm.TagID;
        DELETE ts FROM dbo.TargetScore ts JOIN @Tags x ON x.TagID = ts.TagID;

        UPDATE cp SET cp.Point = CAST(ROUND(remaining.AveragePoint, 2) AS decimal(5, 2))
        FROM dbo.CompetencyPoint cp
        JOIN @AffectedStudents a ON a.StudentID = cp.StudentID AND a.Grade = cp.Grade
        CROSS APPLY (
            SELECT AVG(CAST(tm.OfficialPoint AS decimal(18, 4))) AS AveragePoint
            FROM dbo.TagsMastery tm
            JOIN dbo.TagTopic t ON t.TagID = tm.TagID
            JOIN dbo.TagTopic p ON p.TagID = t.ParentTagID
            WHERE tm.StudentID = a.StudentID AND t.Grade = a.Grade AND p.Grade = a.Grade
              AND t.IsActive = 1 AND p.IsActive = 1 AND p.ParentTagID IS NULL
        ) remaining;

        DELETE t FROM dbo.TagTopic t JOIN @Tags x ON x.TagID = t.TagID
        WHERE t.ParentTagID IN (SELECT TagID FROM @Tags);
        DELETE t FROM dbo.TagTopic t JOIN @Tags x ON x.TagID = t.TagID;

        IF EXISTS (SELECT 1 FROM dbo.TagTopic t JOIN @Tags x ON x.TagID = t.TagID)
            OR EXISTS (SELECT 1 FROM dbo.Question q JOIN @Questions x ON x.QuestionID = q.QuestionID)
            OR EXISTS (SELECT 1 FROM dbo.Blueprint b JOIN @Blueprints x ON x.BlueprintID = b.BlueprintID)
            OR EXISTS (SELECT 1 FROM dbo.Test t JOIN @Tests x ON x.TestID = t.TestID)
            OR EXISTS (SELECT 1 FROM dbo.TestSession s JOIN @Sessions x ON x.SessionID = s.SessionID)
            OR EXISTS (SELECT 1 FROM dbo.QuestionReport r JOIN @Reports x ON x.ReportID = r.ReportID)
            THROW 51015, 'Post-delete verification failed; transaction will roll back.', 1;

        SELECT a.StudentID, a.Grade, cp.Point AS RecalculatedCompetencyPoint
        FROM @AffectedStudents a
        LEFT JOIN dbo.CompetencyPoint cp ON cp.StudentID = a.StudentID AND cp.Grade = a.Grade
        ORDER BY a.StudentID, a.Grade;
    END;

    IF @Commit = 1
    BEGIN
        COMMIT TRANSACTION;
        SELECT N'COMMITTED' AS Outcome;
    END
    ELSE
    BEGIN
        ROLLBACK TRANSACTION;
        SELECT CASE WHEN @RunDeletes = 1 THEN N'REHEARSAL_ROLLED_BACK'
                    ELSE N'PREVIEW_ONLY' END AS Outcome;
    END;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
    THROW;
END CATCH;
