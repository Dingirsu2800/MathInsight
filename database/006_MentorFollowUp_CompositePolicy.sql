SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.TestQuestion', N'GradingPolicyVersion') IS NULL
    ALTER TABLE dbo.TestQuestion
        ADD GradingPolicyVersion INT NOT NULL
            CONSTRAINT DF_TestQuestion_GradingPolicyVersion DEFAULT (1) WITH VALUES;

IF COL_LENGTH(N'dbo.Notification', N'DeduplicationKey') IS NULL
    ALTER TABLE dbo.Notification ADD DeduplicationKey VARCHAR(160) NULL;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UQ_Notification_User_DeduplicationKey'
      AND object_id = OBJECT_ID(N'dbo.Notification'))
    CREATE UNIQUE INDEX UQ_Notification_User_DeduplicationKey
        ON dbo.Notification (UserID, DeduplicationKey)
        WHERE DeduplicationKey IS NOT NULL;

IF OBJECT_ID(N'dbo.QuestionReportIncident', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.QuestionReportIncident (
        IncidentID VARCHAR(36) NOT NULL,
        QuestionID VARCHAR(36) NOT NULL,
        QuestionVersionID VARCHAR(36) NOT NULL,
        Revision INT NOT NULL CONSTRAINT DF_QuestionReportIncident_Revision DEFAULT (0),
        Status VARCHAR(30) NOT NULL CONSTRAINT DF_QuestionReportIncident_Status DEFAULT ('Open'),
        RequiresAdminReview BIT NOT NULL CONSTRAINT DF_QuestionReportIncident_RequiresAdminReview DEFAULT (0),
        AssignedAdminID VARCHAR(36) NULL,
        SubmittedCorrectionVersionID VARCHAR(36) NULL,
        ProposedResolutionAction VARCHAR(30) NULL,
        ApprovedResolutionAction VARCHAR(30) NULL,
        AdjustmentStatus VARCHAR(30) NULL,
        CreatedTime DATETIME2(0) NOT NULL,
        UpdatedTime DATETIME2(0) NULL,
        CONSTRAINT PK_QuestionReportIncident PRIMARY KEY (IncidentID),
        CONSTRAINT UQ_QuestionReportIncident_Question_Version UNIQUE (QuestionID, QuestionVersionID),
        CONSTRAINT FK_QuestionReportIncident_Question_QuestionID FOREIGN KEY (QuestionID) REFERENCES dbo.Question (QuestionID),
        CONSTRAINT FK_QuestionReportIncident_QuestionVersion_QuestionVersionID FOREIGN KEY (QuestionVersionID) REFERENCES dbo.QuestionVersion (VersionID)
    );
END;

IF COL_LENGTH(N'dbo.QuestionReport', N'IncidentID') IS NULL
    ALTER TABLE dbo.QuestionReport ADD IncidentID VARCHAR(36) NULL;

-- Existing reports have no reliable snapshot mapping. Keep them nullable instead of
-- inventing a version; new version-scoped reports are protected by the filtered key below.
IF COL_LENGTH(N'dbo.QuestionReport', N'SessionID') IS NULL
    ALTER TABLE dbo.QuestionReport ADD SessionID VARCHAR(36) NULL;

IF COL_LENGTH(N'dbo.QuestionReport', N'QuestionVersionID') IS NULL
    ALTER TABLE dbo.QuestionReport ADD QuestionVersionID VARCHAR(36) NULL;

IF COL_LENGTH(N'dbo.QuestionReport', N'ResolutionAction') IS NULL
    ALTER TABLE dbo.QuestionReport ADD ResolutionAction VARCHAR(30) NULL;

IF COL_LENGTH(N'dbo.QuestionReport', N'ScoreAdjustedTime') IS NULL
    ALTER TABLE dbo.QuestionReport ADD ScoreAdjustedTime DATETIME2(0) NULL;
GO

IF OBJECT_ID(N'dbo.QuestionReportLegacyAudit', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.QuestionReportLegacyAudit (
        AuditID BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_QuestionReportLegacyAudit PRIMARY KEY,
        ReportID VARCHAR(36) NOT NULL,
        IssueCode VARCHAR(64) NOT NULL,
        OriginalQuestionVersionID VARCHAR(36) NULL,
        AuditedTime DATETIME2(0) NOT NULL CONSTRAINT DF_QuestionReportLegacyAudit_AuditedTime DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_QuestionReportLegacyAudit_Report_Issue UNIQUE (ReportID, IssueCode)
    );
END;

-- A historical student report with a session can be tied to the immutable test snapshot.
-- Never infer a version from the current mutable Question row. Map only one canonical
-- report per reporter/version so rerunning after the filtered unique index exists cannot
-- recreate a duplicate key collision.
;WITH SessionBackfillCandidates AS (
    SELECT report.ReportID,
           report.QuestionVersionID,
           testQuestion.QuestionVersionID AS SnapshotQuestionVersionID,
           ROW_NUMBER() OVER (
               PARTITION BY report.ReporterAccountID, testQuestion.QuestionVersionID
               ORDER BY report.CreatedTime, report.ReportID) AS RowNumber
    FROM dbo.QuestionReport report
    INNER JOIN dbo.TestSession session ON session.SessionID = report.SessionID
    INNER JOIN dbo.TestQuestion testQuestion
        ON testQuestion.TestID = session.TestID
       AND testQuestion.QuestionID = report.QuestionID
    WHERE report.QuestionVersionID IS NULL
      AND report.SessionID IS NOT NULL
      AND testQuestion.QuestionVersionID IS NOT NULL
)
INSERT INTO dbo.QuestionReportLegacyAudit (ReportID, IssueCode, OriginalQuestionVersionID)
SELECT candidate.ReportID, N'DUPLICATE_REPORTER_VERSION', candidate.SnapshotQuestionVersionID
FROM SessionBackfillCandidates candidate
WHERE candidate.RowNumber > 1
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.QuestionReportLegacyAudit audit
      WHERE audit.ReportID = candidate.ReportID
        AND audit.IssueCode = N'DUPLICATE_REPORTER_VERSION');

;WITH SessionBackfillCandidates AS (
    SELECT report.ReportID,
           testQuestion.QuestionVersionID AS SnapshotQuestionVersionID,
           ROW_NUMBER() OVER (
               PARTITION BY report.ReporterAccountID, testQuestion.QuestionVersionID
               ORDER BY report.CreatedTime, report.ReportID) AS RowNumber
    FROM dbo.QuestionReport report
    INNER JOIN dbo.TestSession session ON session.SessionID = report.SessionID
    INNER JOIN dbo.TestQuestion testQuestion
        ON testQuestion.TestID = session.TestID
       AND testQuestion.QuestionID = report.QuestionID
    WHERE report.QuestionVersionID IS NULL
      AND report.SessionID IS NOT NULL
      AND testQuestion.QuestionVersionID IS NOT NULL
)
UPDATE report
SET QuestionVersionID = candidate.SnapshotQuestionVersionID
FROM dbo.QuestionReport report
INNER JOIN SessionBackfillCandidates candidate ON candidate.ReportID = report.ReportID
WHERE candidate.RowNumber = 1
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.QuestionReport existing
      WHERE existing.ReporterAccountID = report.ReporterAccountID
        AND existing.QuestionVersionID = candidate.SnapshotQuestionVersionID);

;WITH RankedDuplicateReports AS (
    SELECT ReportID,
           QuestionVersionID,
           ROW_NUMBER() OVER (
               PARTITION BY ReporterAccountID, QuestionVersionID
               ORDER BY CreatedTime, ReportID) AS RowNumber
    FROM dbo.QuestionReport
    WHERE QuestionVersionID IS NOT NULL
)
INSERT INTO dbo.QuestionReportLegacyAudit (ReportID, IssueCode, OriginalQuestionVersionID)
SELECT duplicate.ReportID, N'DUPLICATE_REPORTER_VERSION', duplicate.QuestionVersionID
FROM RankedDuplicateReports duplicate
WHERE duplicate.RowNumber > 1
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.QuestionReportLegacyAudit audit
      WHERE audit.ReportID = duplicate.ReportID
        AND audit.IssueCode = N'DUPLICATE_REPORTER_VERSION');

;WITH RankedDuplicateReports AS (
    SELECT ReportID,
           ROW_NUMBER() OVER (
               PARTITION BY ReporterAccountID, QuestionVersionID
               ORDER BY CreatedTime, ReportID) AS RowNumber
    FROM dbo.QuestionReport
    WHERE QuestionVersionID IS NOT NULL
)
UPDATE report
SET QuestionVersionID = NULL
FROM dbo.QuestionReport report
INNER JOIN RankedDuplicateReports duplicate ON duplicate.ReportID = report.ReportID
WHERE duplicate.RowNumber > 1;

INSERT INTO dbo.QuestionReportLegacyAudit (ReportID, IssueCode, OriginalQuestionVersionID)
SELECT report.ReportID, N'VERSION_UNAVAILABLE', NULL
FROM dbo.QuestionReport report
WHERE report.QuestionVersionID IS NULL
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.QuestionReportLegacyAudit audit
      WHERE audit.ReportID = report.ReportID
        AND audit.IssueCode = N'DUPLICATE_REPORTER_VERSION')
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.QuestionReportLegacyAudit audit
      WHERE audit.ReportID = report.ReportID
        AND audit.IssueCode = N'VERSION_UNAVAILABLE');
GO

IF COL_LENGTH(N'dbo.QuestionReportIncident', N'SubmissionKey') IS NULL
    ALTER TABLE dbo.QuestionReportIncident ADD SubmissionKey VARCHAR(64) NULL;
GO

IF COL_LENGTH(N'dbo.QuestionReportIncident', N'SubmissionPayloadHash') IS NULL
    ALTER TABLE dbo.QuestionReportIncident ADD SubmissionPayloadHash VARCHAR(64) NULL;

IF COL_LENGTH(N'dbo.QuestionReport', N'ProposedStatus') IS NULL
    ALTER TABLE dbo.QuestionReport ADD ProposedStatus VARCHAR(20) NULL;

IF COL_LENGTH(N'dbo.QuestionReport', N'ProposedReviewNote') IS NULL
    ALTER TABLE dbo.QuestionReport ADD ProposedReviewNote NVARCHAR(MAX) NULL;
GO

IF OBJECT_ID(N'dbo.ScoreAdjustmentWork', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScoreAdjustmentWork (
        WorkID VARCHAR(36) NOT NULL,
        ReportID VARCHAR(36) NOT NULL,
        IncidentID VARCHAR(36) NULL,
        SessionID VARCHAR(36) NOT NULL,
        GradeRevision INT NOT NULL,
        EventPayload NVARCHAR(MAX) NOT NULL,
        Status VARCHAR(20) NOT NULL CONSTRAINT DF_ScoreAdjustmentWork_Status DEFAULT ('Pending'),
        AttemptCount INT NOT NULL CONSTRAINT DF_ScoreAdjustmentWork_AttemptCount DEFAULT (0),
        LastError NVARCHAR(2000) NULL,
        CreatedTime DATETIME2(0) NOT NULL,
        CompletedTime DATETIME2(0) NULL,
        CONSTRAINT PK_ScoreAdjustmentWork PRIMARY KEY (WorkID),
        CONSTRAINT FK_ScoreAdjustmentWork_Report_ReportID FOREIGN KEY (ReportID) REFERENCES dbo.QuestionReport (ReportID)
    );
END;
GO

IF EXISTS (
    SELECT 1 FROM sys.key_constraints
    WHERE name = N'UQ_ScoreAdjustmentWork_Report_Session_Revision'
      AND parent_object_id = OBJECT_ID(N'dbo.ScoreAdjustmentWork'))
    ALTER TABLE dbo.ScoreAdjustmentWork DROP CONSTRAINT UQ_ScoreAdjustmentWork_Report_Session_Revision;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UQ_ScoreAdjustmentWork_Incident_Session_Revision'
      AND object_id = OBJECT_ID(N'dbo.ScoreAdjustmentWork'))
    CREATE UNIQUE INDEX UQ_ScoreAdjustmentWork_Incident_Session_Revision
        ON dbo.ScoreAdjustmentWork (IncidentID, SessionID, GradeRevision)
        WHERE IncidentID IS NOT NULL;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UQ_ScoreAdjustmentWork_LegacyReport_Session_Revision'
      AND object_id = OBJECT_ID(N'dbo.ScoreAdjustmentWork'))
    CREATE UNIQUE INDEX UQ_ScoreAdjustmentWork_LegacyReport_Session_Revision
        ON dbo.ScoreAdjustmentWork (ReportID, SessionID, GradeRevision)
        WHERE IncidentID IS NULL;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_ScoreAdjustmentWork_Status_CreatedTime'
      AND object_id = OBJECT_ID(N'dbo.ScoreAdjustmentWork'))
    CREATE INDEX IX_ScoreAdjustmentWork_Status_CreatedTime
        ON dbo.ScoreAdjustmentWork (Status, CreatedTime);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_QuestionReport_Incident_IncidentID'
      AND parent_object_id = OBJECT_ID(N'dbo.QuestionReport'))
    ALTER TABLE dbo.QuestionReport
        ADD CONSTRAINT FK_QuestionReport_Incident_IncidentID
        FOREIGN KEY (IncidentID) REFERENCES dbo.QuestionReportIncident (IncidentID);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_QuestionReport_Incident_Status'
      AND object_id = OBJECT_ID(N'dbo.QuestionReport'))
    CREATE INDEX IX_QuestionReport_Incident_Status
        ON dbo.QuestionReport (IncidentID, Status)
        WHERE IncidentID IS NOT NULL;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UQ_QuestionReport_Reporter_Version'
      AND object_id = OBJECT_ID(N'dbo.QuestionReport'))
    CREATE UNIQUE INDEX UQ_QuestionReport_Reporter_Version
        ON dbo.QuestionReport (ReporterAccountID, QuestionVersionID)
        WHERE QuestionVersionID IS NOT NULL;

IF EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_BlueprintSection_CompositePartMetadata'
      AND parent_object_id = OBJECT_ID(N'dbo.BlueprintSection'))
    ALTER TABLE dbo.BlueprintSection DROP CONSTRAINT CK_BlueprintSection_CompositePartMetadata;

ALTER TABLE dbo.BlueprintSection WITH CHECK
    ADD CONSTRAINT CK_BlueprintSection_CompositePartMetadata CHECK (
        ([QuestionType] = 'Composite' AND [PartCountPerQuestion] IS NULL AND [ScoringRule] IN ('TieredTrueFalse', 'WeightedParts')) OR
        ([QuestionType] <> 'Composite' AND [PartCountPerQuestion] IS NULL AND [ScoringRule] = 'AllOrNothing'));

COMMIT TRANSACTION;
