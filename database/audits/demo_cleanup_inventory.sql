-- Read-only inventory for the eight non-seed topics reviewed on 2026-09-25.
-- Review every result set before choosing IDs for any cleanup.
SET NOCOUNT ON;

DECLARE @CandidateTagJson nvarchar(max) = N'[
    "4d5b7fe0-f0b3-49f8-a658-86b74efc7e6e",
    "705109d1-5d77-4b82-823d-fd41c71418f7",
    "de4818df-1906-45b9-a4ec-8af002807671",
    "bcde90ee-85fe-4054-974d-e2e08bc21d75",
    "89cf7e58-a63e-40e3-9706-ab54eac24873",
    "da0b7bac-2dfe-47d8-8766-31b290577024",
    "3c33fb39-c2ea-43e2-8725-67de22a4bcb9",
    "8421645b-7cd6-460b-a0a6-1d669ff87921"
]';

-- 1. Candidate topics and direct references (not descendant totals).
SELECT t.TagID, p.TagName AS ParentTopic, t.TagName, t.IsActive,
    (SELECT COUNT(*) FROM dbo.QuestionTopic qt WHERE qt.TagID = t.TagID) AS Questions,
    (SELECT COUNT(DISTINCT d.BlueprintID) FROM dbo.BlueprintDetail d WHERE d.TagID = t.TagID) AS Blueprints,
    (SELECT COUNT(*) FROM dbo.Lecture l WHERE l.TagID = t.TagID) AS Lectures,
    (SELECT COUNT(*) FROM dbo.TagsMastery m WHERE m.TagID = t.TagID) AS MasteryRows,
    (SELECT COUNT(*) FROM dbo.StudentTopicSessionResult r WHERE r.TagID = t.TagID) AS SessionResultRows
FROM OPENJSON(@CandidateTagJson) WITH (TagID varchar(36) '$') c
JOIN dbo.TagTopic t ON t.TagID = c.TagID
LEFT JOIN dbo.TagTopic p ON p.TagID = t.ParentTagID
ORDER BY p.TagName, t.TagName;

-- 2. Never delete a question with another topic link without manual review.
SELECT q.QuestionID, q.Status, q.IsActive,
    LEFT(q.QuestionContent, 120) AS QuestionPreview,
    CASE WHEN EXISTS (
        SELECT 1 FROM dbo.QuestionTopic otherLink
        LEFT JOIN OPENJSON(@CandidateTagJson) WITH (TagID varchar(36) '$') c ON c.TagID = otherLink.TagID
        WHERE otherLink.QuestionID = q.QuestionID AND c.TagID IS NULL
    ) THEN 1 ELSE 0 END AS HasOtherTopicLinks,
    (SELECT COUNT(*) FROM dbo.TestQuestion tq WHERE tq.QuestionID = q.QuestionID) AS TestUses,
    (SELECT COUNT(*) FROM dbo.QuestionReport qr WHERE qr.QuestionID = q.QuestionID) AS Reports
FROM dbo.Question q
WHERE EXISTS (
    SELECT 1 FROM dbo.QuestionTopic qt
    JOIN OPENJSON(@CandidateTagJson) WITH (TagID varchar(36) '$') c ON c.TagID = qt.TagID
    WHERE qt.QuestionID = q.QuestionID
)
ORDER BY HasOtherTopicLinks DESC, q.QuestionID;

-- 3. Blueprints that contain at least one candidate topic.
SELECT b.BlueprintID, b.BlueprintName, b.Status, b.CreatedTime,
    CASE WHEN EXISTS (
        SELECT 1 FROM dbo.BlueprintDetail otherDetail
        LEFT JOIN OPENJSON(@CandidateTagJson) WITH (TagID varchar(36) '$') c ON c.TagID = otherDetail.TagID
        WHERE otherDetail.BlueprintID = b.BlueprintID AND c.TagID IS NULL
    ) THEN 1 ELSE 0 END AS HasOtherTopics,
    (SELECT COUNT(*) FROM dbo.Test x WHERE x.BlueprintID = b.BlueprintID) AS GeneratedTests,
    (SELECT COUNT(*) FROM dbo.TestSession s
     JOIN dbo.Test x ON x.TestID = s.TestID
     WHERE x.BlueprintID = b.BlueprintID) AS Sessions
FROM dbo.Blueprint b
WHERE EXISTS (
    SELECT 1 FROM dbo.BlueprintDetail d
    JOIN OPENJSON(@CandidateTagJson) WITH (TagID varchar(36) '$') c ON c.TagID = d.TagID
    WHERE d.BlueprintID = b.BlueprintID
)
ORDER BY b.CreatedTime DESC, b.BlueprintID;

-- 4. Tests using candidate-topic questions or directly recommending a candidate topic.
-- Includes topic practice tests without a blueprint.
SELECT x.TestID, x.TestName, x.TestMode, x.BlueprintID, x.TestStatus, x.CreatedTime,
    (SELECT COUNT(*) FROM dbo.TestSession s WHERE s.TestID = x.TestID) AS Sessions,
    (SELECT COUNT(*) FROM dbo.TestSession s
     WHERE s.TestID = x.TestID AND s.Status = 'InProgress') AS InProgressSessions
FROM dbo.Test x
WHERE EXISTS (
    SELECT 1 FROM dbo.TestQuestion tq
    WHERE tq.TestID = x.TestID
      AND (
          tq.RecommendedForTagID IN (SELECT TagID FROM OPENJSON(@CandidateTagJson) WITH (TagID varchar(36) '$'))
          OR EXISTS (
              SELECT 1 FROM dbo.QuestionTopic qt
              JOIN OPENJSON(@CandidateTagJson) WITH (TagID varchar(36) '$') c ON c.TagID = qt.TagID
              WHERE qt.QuestionID = tq.QuestionID
          )
      )
)
ORDER BY x.CreatedTime DESC, x.TestID;

-- 5. Foreign-key dependency map from cleanup roots, using the live database schema.
-- This reports table relationships only; it does not modify or count data.
;WITH DependentTables AS (
    SELECT o.object_id AS TableID,
        CAST(N'|' + CONVERT(nvarchar(20), o.object_id) + N'|' AS nvarchar(max)) AS Visited,
        0 AS Depth
    FROM sys.objects o
    WHERE o.type = 'U'
      AND SCHEMA_NAME(o.schema_id) = 'dbo'
      AND o.name IN (N'TagTopic', N'Question', N'Blueprint', N'Test', N'TestSession')

    UNION ALL

    SELECT fk.parent_object_id,
        CAST(d.Visited + CONVERT(nvarchar(20), fk.parent_object_id) + N'|' AS nvarchar(max)),
        d.Depth + 1
    FROM DependentTables d
    JOIN sys.foreign_keys fk ON fk.referenced_object_id = d.TableID
    WHERE d.Depth < 6
      AND CHARINDEX(N'|' + CONVERT(nvarchar(20), fk.parent_object_id) + N'|', d.Visited) = 0
)
SELECT DISTINCT
    OBJECT_SCHEMA_NAME(fk.parent_object_id) + N'.' + OBJECT_NAME(fk.parent_object_id) AS DependentTable,
    fk.name AS ForeignKey,
    OBJECT_SCHEMA_NAME(fk.referenced_object_id) + N'.' + OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable,
    fk.delete_referential_action_desc AS OnDelete
FROM DependentTables d
JOIN sys.foreign_keys fk ON fk.referenced_object_id = d.TableID
ORDER BY ReferencedTable, DependentTable, ForeignKey
OPTION (MAXRECURSION 100);

-- 6. A candidate test may also contain protected, seed-topic questions.
;WITH CandidateQuestions AS (
    SELECT DISTINCT qt.QuestionID
    FROM dbo.QuestionTopic qt
    JOIN OPENJSON(@CandidateTagJson) WITH (TagID varchar(36) '$') c ON c.TagID = qt.TagID
),
CandidateBlueprints AS (
    SELECT DISTINCT bd.BlueprintID
    FROM dbo.BlueprintDetail bd
    JOIN OPENJSON(@CandidateTagJson) WITH (TagID varchar(36) '$') c ON c.TagID = bd.TagID
),
CandidateTests AS (
    SELECT t.TestID, t.TestName
    FROM dbo.Test t
    WHERE t.BlueprintID IN (SELECT BlueprintID FROM CandidateBlueprints)
       OR EXISTS (
           SELECT 1 FROM dbo.TestQuestion tq
           WHERE tq.TestID = t.TestID
             AND (tq.QuestionID IN (SELECT QuestionID FROM CandidateQuestions)
                  OR tq.RecommendedForTagID IN (
                      SELECT TagID FROM OPENJSON(@CandidateTagJson) WITH (TagID varchar(36) '$')))
       )
)
SELECT t.TestID, t.TestName,
    (SELECT COUNT(*) FROM dbo.TestQuestion tq WHERE tq.TestID = t.TestID) AS TotalQuestionLinks,
    (SELECT COUNT(*) FROM dbo.TestQuestion tq
     WHERE tq.TestID = t.TestID AND tq.QuestionID IN (SELECT QuestionID FROM CandidateQuestions)) AS CandidateQuestionLinks,
    (SELECT COUNT(*) FROM dbo.TestQuestion tq
     WHERE tq.TestID = t.TestID AND tq.QuestionID NOT IN (SELECT QuestionID FROM CandidateQuestions)) AS OtherQuestionLinks,
    (SELECT COUNT(*) FROM dbo.TestSession s WHERE s.TestID = t.TestID) AS Sessions
FROM CandidateTests t
ORDER BY t.TestID;

-- 7. Reports and adjustment jobs require a decision before deleting history.
;WITH CandidateQuestions AS (
    SELECT DISTINCT qt.QuestionID
    FROM dbo.QuestionTopic qt
    JOIN OPENJSON(@CandidateTagJson) WITH (TagID varchar(36) '$') c ON c.TagID = qt.TagID
)
SELECT qr.ReportID, qr.QuestionID, qr.Status AS ReportStatus, qr.SessionID,
    w.WorkId, w.Status AS AdjustmentStatus, w.AttemptCount
FROM dbo.QuestionReport qr
LEFT JOIN dbo.ScoreAdjustmentWork w ON w.ReportID = qr.ReportID
WHERE qr.QuestionID IN (SELECT QuestionID FROM CandidateQuestions)
ORDER BY qr.QuestionID, qr.ReportID, w.WorkId;

-- 8. Removing demo mastery rows does not automatically refresh CompetencyPoint.
;WITH AffectedStudents AS (
    SELECT DISTINCT tm.StudentID, t.Grade
    FROM dbo.TagsMastery tm
    JOIN dbo.TagTopic t ON t.TagID = tm.TagID
    JOIN OPENJSON(@CandidateTagJson) WITH (TagID varchar(36) '$') c ON c.TagID = tm.TagID
)
SELECT a.StudentID, a.Grade, cp.Point AS CurrentCompetencyPoint,
    (SELECT COUNT(*) FROM dbo.TagsMastery tm
     JOIN dbo.TagTopic t ON t.TagID = tm.TagID
     JOIN dbo.TagTopic p ON p.TagID = t.ParentTagID
     WHERE tm.StudentID = a.StudentID AND t.Grade = a.Grade
       AND p.Grade = a.Grade AND t.IsActive = 1 AND p.IsActive = 1
       AND p.ParentTagID IS NULL
       AND t.TagID NOT IN (SELECT TagID FROM OPENJSON(@CandidateTagJson) WITH (TagID varchar(36) '$'))
    ) AS RemainingActiveChildMasteries,
    (SELECT AVG(CAST(tm.OfficialPoint AS decimal(18, 4))) FROM dbo.TagsMastery tm
     JOIN dbo.TagTopic t ON t.TagID = tm.TagID
     JOIN dbo.TagTopic p ON p.TagID = t.ParentTagID
     WHERE tm.StudentID = a.StudentID AND t.Grade = a.Grade
       AND p.Grade = a.Grade AND t.IsActive = 1 AND p.IsActive = 1
       AND p.ParentTagID IS NULL
       AND t.TagID NOT IN (SELECT TagID FROM OPENJSON(@CandidateTagJson) WITH (TagID varchar(36) '$'))
    ) AS RemainingCompetencyAverage
FROM AffectedStudents a
LEFT JOIN dbo.CompetencyPoint cp ON cp.StudentID = a.StudentID AND cp.Grade = a.Grade
ORDER BY a.StudentID, a.Grade;

-- 9. ActivityLog is session-linked, but streaks and earned badges are not.
;WITH CandidateQuestions AS (
    SELECT DISTINCT qt.QuestionID FROM dbo.QuestionTopic qt
    JOIN OPENJSON(@CandidateTagJson) WITH (TagID varchar(36) '$') c ON c.TagID = qt.TagID
), CandidateBlueprints AS (
    SELECT DISTINCT d.BlueprintID FROM dbo.BlueprintDetail d
    JOIN OPENJSON(@CandidateTagJson) WITH (TagID varchar(36) '$') c ON c.TagID = d.TagID
), CandidateTests AS (
    SELECT t.TestID FROM dbo.Test t
    WHERE t.BlueprintID IN (SELECT BlueprintID FROM CandidateBlueprints)
       OR EXISTS (SELECT 1 FROM dbo.TestQuestion tq WHERE tq.TestID = t.TestID
                  AND (tq.QuestionID IN (SELECT QuestionID FROM CandidateQuestions)
                       OR tq.RecommendedForTagID IN (
                           SELECT TagID FROM OPENJSON(@CandidateTagJson) WITH (TagID varchar(36) '$'))))
)
SELECT s.SessionID, s.StudentID,
    (SELECT COUNT(*) FROM dbo.ActivityLog a WHERE a.TestSessionID = s.SessionID) AS SessionActivityLogs,
    streak.CurrentStreak, streak.LongestStreak, streak.LastActivityDate,
    (SELECT COUNT(*) FROM dbo.StudentBadge b WHERE b.StudentID = s.StudentID) AS EarnedBadges
FROM dbo.TestSession s
JOIN CandidateTests t ON t.TestID = s.TestID
LEFT JOIN dbo.StudyStreak streak ON streak.StudentID = s.StudentID
ORDER BY s.StudentID, s.SessionID;

-- 10. Notifications have no FK to their target; only exact embedded IDs are listed.
;WITH CandidateQuestions AS (
    SELECT DISTINCT qt.QuestionID FROM dbo.QuestionTopic qt
    JOIN OPENJSON(@CandidateTagJson) WITH (TagID varchar(36) '$') c ON c.TagID = qt.TagID
), CandidateBlueprints AS (
    SELECT DISTINCT d.BlueprintID FROM dbo.BlueprintDetail d
    JOIN OPENJSON(@CandidateTagJson) WITH (TagID varchar(36) '$') c ON c.TagID = d.TagID
), CandidateTests AS (
    SELECT t.TestID FROM dbo.Test t
    WHERE t.BlueprintID IN (SELECT BlueprintID FROM CandidateBlueprints)
       OR EXISTS (SELECT 1 FROM dbo.TestQuestion tq WHERE tq.TestID = t.TestID
                  AND (tq.QuestionID IN (SELECT QuestionID FROM CandidateQuestions)
                       OR tq.RecommendedForTagID IN (
                           SELECT TagID FROM OPENJSON(@CandidateTagJson) WITH (TagID varchar(36) '$'))))
), CandidateSessions AS (
    SELECT s.SessionID FROM dbo.TestSession s JOIN CandidateTests t ON t.TestID = s.TestID
), CandidateReports AS (
    SELECT r.ReportID FROM dbo.QuestionReport r
    JOIN CandidateQuestions q ON q.QuestionID = r.QuestionID
), EntityIds AS (
    SELECT TagID AS EntityID FROM OPENJSON(@CandidateTagJson) WITH (TagID varchar(36) '$')
    UNION SELECT QuestionID FROM CandidateQuestions
    UNION SELECT BlueprintID FROM CandidateBlueprints
    UNION SELECT TestID FROM CandidateTests
    UNION SELECT SessionID FROM CandidateSessions
    UNION SELECT ReportID FROM CandidateReports
)
SELECT DISTINCT n.NotificationID, n.UserID, n.Link, n.DeduplicationKey
FROM dbo.Notification n
JOIN EntityIds e ON CHARINDEX(e.EntityID, COALESCE(n.Link, '')) > 0
                 OR CHARINDEX(e.EntityID, COALESCE(n.DeduplicationKey, '')) > 0
ORDER BY n.NotificationID;
