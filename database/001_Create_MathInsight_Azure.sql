/*
==========================================================
 MathInsight consolidated schema creation script
 Target: a new, empty Azure SQL Database / SQL Server database
 Consolidates the current schema plus migrations 001, 002 and 003.
 Date: 12/07/2026
==========================================================
*/

/*
 Run this script while connected directly to the intended database.
 It intentionally contains no CREATE DATABASE, DROP DATABASE, or USE statement,
 so it is compatible with Azure SQL Database.
 This is a clean-install script, not an update script for an existing database.
*/

-- Required by SQL Server for filtered indexes and deterministic schema behavior.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO



-- Create Table: Account
CREATE TABLE [Account] (
    [AccountID] VARCHAR(36) NOT NULL,
    [Username] NVARCHAR(50) NOT NULL UNIQUE,
    [PasswordHash] VARCHAR(255) NOT NULL,
    [Email] VARCHAR(100) NOT NULL UNIQUE,
    [FirstName] NVARCHAR(50) NOT NULL,
    [LastName] NVARCHAR(50) NOT NULL,
    [PhoneNumber] VARCHAR(20) NULL,
    [DateOfBirth] DATE NULL,
    [AvatarUrl] VARCHAR(255) NULL,
    [RoleID] VARCHAR(36) NOT NULL,
    -- Self-registered accounts remain inactive until email confirmation succeeds.
    [isActive] BIT NOT NULL DEFAULT 0,
    [CreatedTime] DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    [GoogleSubID] VARCHAR(255) NULL,
    [GoogleEmail] VARCHAR(100) NULL,
    CONSTRAINT [PK_Account] PRIMARY KEY ([AccountID])
);
GO

-- Create Table: ActivityLog
CREATE TABLE [ActivityLog] (
    [ActivityLogID] VARCHAR(36) NOT NULL,
    [StudentID] VARCHAR(36) NOT NULL,
    [ActivityType] VARCHAR(50) NOT NULL,
    [TestSessionID] VARCHAR(36) NULL,
    [LectureID] VARCHAR(36) NULL,
    [MaterialID] VARCHAR(36) NULL,
    [DurationSeconds] INT NULL DEFAULT 0,
    [ActivityDate] DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_ActivityLog] PRIMARY KEY ([ActivityLogID]),
    CONSTRAINT [CK_ActivityLog_DurationSeconds] CHECK ([DurationSeconds] IS NULL OR [DurationSeconds] >= 0)
);
GO

-- Create Table: Answer
CREATE TABLE [Answer] (
    [AnswerID] VARCHAR(36) NOT NULL,
    [QuestionID] VARCHAR(36) NOT NULL,
    [AnswerContent] NVARCHAR(MAX) NOT NULL,
    [IsCorrect] BIT NOT NULL DEFAULT 0,
    CONSTRAINT [PK_Answer] PRIMARY KEY ([AnswerID])
);
GO

-- Create Table: Badge
CREATE TABLE [Badge] (
    [BadgeID] VARCHAR(36) NOT NULL,
    [BadgeName] NVARCHAR(100) NOT NULL UNIQUE,
    [Description] NVARCHAR(255) NOT NULL,
    [IconUrl] VARCHAR(255) NULL,
    [ConditionType] VARCHAR(50) NOT NULL,
    [ConditionValue] INT NOT NULL,
    [CreatedTime] DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_Badge] PRIMARY KEY ([BadgeID]),
    CONSTRAINT [CK_Badge_ConditionValue] CHECK ([ConditionValue] >= 0)
);
GO

-- Create Table: Blueprint
CREATE TABLE [Blueprint] (
    [BlueprintID] VARCHAR(36) NOT NULL,
    [BlueprintName] NVARCHAR(100) NOT NULL,
    [Grade] INT NOT NULL DEFAULT 10,
    [TotalQuestions] INT NOT NULL DEFAULT 0,
    [DurationMinutes] INT NOT NULL DEFAULT 0,
    [ExpertID] VARCHAR(36) NOT NULL,
    [Status] VARCHAR(20) NOT NULL DEFAULT 'Draft',
    [ApprovedBy] VARCHAR(36) NULL,
    [ReviewNote] NVARCHAR(MAX) NULL,
    [ReviewTime] DATETIME2(0) NULL,
    CONSTRAINT [PK_Blueprint] PRIMARY KEY ([BlueprintID]),
    CONSTRAINT [CK_Blueprint_Grade] CHECK ([Grade] IN (10, 11, 12)),
    CONSTRAINT [CK_Blueprint_TotalQuestions] CHECK ([TotalQuestions] >= 0),
    CONSTRAINT [CK_Blueprint_DurationMinutes] CHECK ([DurationMinutes] >= 0),
    CONSTRAINT [CK_Blueprint_Status] CHECK ([Status] IN ('Draft', 'PendingReview', 'Approved', 'Rejected', 'Active', 'Deactivated'))
);
GO

IF OBJECT_ID(N'dbo.Account', N'U') IS NOT NULL
   OR OBJECT_ID(N'dbo.Question', N'U') IS NOT NULL
BEGIN
    THROW 51000, N'Target database is not empty. Use migration scripts for an existing database.', 1;
END;
GO

-- Create Table: BlueprintSection
CREATE TABLE [BlueprintSection] (
    [BlueprintSectionID] VARCHAR(36) NOT NULL,
    [BlueprintID] VARCHAR(36) NOT NULL,
    [SectionOrder] INT NOT NULL DEFAULT 1,
    [SectionCode] NVARCHAR(20) NULL,
    [SectionName] NVARCHAR(100) NOT NULL,
    [QuestionType] VARCHAR(30) NOT NULL DEFAULT 'SingleChoice',
    [InstructionText] NVARCHAR(MAX) NULL,
    [TotalQuestions] INT NOT NULL DEFAULT 0,
    [DefaultPointPerQuestion] DECIMAL(4,2) NOT NULL DEFAULT 0.00,
    [DefaultPointPerPart] DECIMAL(4,2) NULL,
    [PartCountPerQuestion] INT NULL,
    CONSTRAINT [PK_BlueprintSection] PRIMARY KEY ([BlueprintSectionID]),
    CONSTRAINT [UQ_BlueprintSection_Blueprint_Order] UNIQUE ([BlueprintID], [SectionOrder]),
    CONSTRAINT [UQ_BlueprintSection_ID_Blueprint] UNIQUE ([BlueprintSectionID], [BlueprintID]),
    CONSTRAINT [CK_BlueprintSection_Order] CHECK ([SectionOrder] > 0),
    CONSTRAINT [CK_BlueprintSection_QuestionType] CHECK ([QuestionType] IN ('SingleChoice', 'MultipleChoice', 'TrueFalse', 'ShortAnswer', 'Composite')),
    CONSTRAINT [CK_BlueprintSection_TotalQuestions] CHECK ([TotalQuestions] >= 0),
    CONSTRAINT [CK_BlueprintSection_DefaultPointPerQuestion] CHECK ([DefaultPointPerQuestion] >= 0 AND [DefaultPointPerQuestion] <= 10),
    CONSTRAINT [CK_BlueprintSection_DefaultPointPerPart] CHECK ([DefaultPointPerPart] IS NULL OR ([DefaultPointPerPart] >= 0 AND [DefaultPointPerPart] <= 10)),
    CONSTRAINT [CK_BlueprintSection_PartCountPerQuestion] CHECK ([PartCountPerQuestion] IS NULL OR [PartCountPerQuestion] > 0),
    CONSTRAINT [CK_BlueprintSection_CompositePartMetadata] CHECK (
        ([QuestionType] = 'Composite' AND [PartCountPerQuestion] IS NOT NULL AND [DefaultPointPerPart] IS NOT NULL)
        OR
        ([QuestionType] <> 'Composite' AND [PartCountPerQuestion] IS NULL AND [DefaultPointPerPart] IS NULL)
    )
);
GO

-- Create Table: BlueprintDetail
CREATE TABLE [BlueprintDetail] (
    [BlueprintDetailID] VARCHAR(36) NOT NULL,
    [BlueprintID] VARCHAR(36) NOT NULL,
    [BlueprintSectionID] VARCHAR(36) NOT NULL,
    [TagID] VARCHAR(36) NOT NULL,
    [DifficultyID] VARCHAR(36) NOT NULL,
    [Quantity] INT NOT NULL DEFAULT 0,
    CONSTRAINT [PK_BlueprintDetail] PRIMARY KEY ([BlueprintDetailID]),
    CONSTRAINT [UQ_BlueprintDetail_Section_Tag_Difficulty] UNIQUE ([BlueprintSectionID], [TagID], [DifficultyID]),
    CONSTRAINT [CK_BlueprintDetail_Quantity] CHECK ([Quantity] >= 0)
);
GO

-- Create Table: CompetencyPoint
CREATE TABLE [CompetencyPoint] (
    [CompetencyID] VARCHAR(36) NOT NULL,
    [StudentID] VARCHAR(36) NOT NULL,
    [Grade] INT NOT NULL DEFAULT 10,
    [Point] DECIMAL(5,2) NOT NULL DEFAULT 0.00,
    CONSTRAINT [PK_CompetencyPoint] PRIMARY KEY ([CompetencyID]),
    CONSTRAINT [UQ_CompetencyPoint_Student_Grade] UNIQUE ([StudentID], [Grade]),
    CONSTRAINT [CK_CompetencyPoint_Grade] CHECK ([Grade] IN (10, 11, 12)),
    CONSTRAINT [CK_CompetencyPoint_Point] CHECK ([Point] >= 0 AND [Point] <= 10)
);
GO

-- Create Table: DiscussionAnswer
CREATE TABLE [DiscussionAnswer] (
    [DiscussionAnswerID] VARCHAR(36) NOT NULL,
    [DiscussionQuestionID] VARCHAR(36) NOT NULL,
    [AccountID] VARCHAR(36) NOT NULL,
    [Content] NVARCHAR(MAX) NOT NULL,
    [CreatedTime] DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    [Status] VARCHAR(20) NOT NULL DEFAULT 'Active',
    [UpdatedTime] DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_DiscussionAnswer] PRIMARY KEY ([DiscussionAnswerID]),
    CONSTRAINT [CK_DiscussionAnswer_Status] CHECK ([Status] IN ('Active', 'Hidden', 'Deleted'))
);
GO

-- Create Table: DiscussionQuestion
CREATE TABLE [DiscussionQuestion] (
    [DiscussionQuestionID] VARCHAR(36) NOT NULL,
    [LectureID] VARCHAR(36) NOT NULL,
    [StudentID] VARCHAR(36) NOT NULL,
    [Title] NVARCHAR(150) NOT NULL,
    [Content] NVARCHAR(MAX) NOT NULL,
    [Status] VARCHAR(20) NOT NULL DEFAULT 'Active',
    [CreatedTime] DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    [UpdatedTime] DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_DiscussionQuestion] PRIMARY KEY ([DiscussionQuestionID]),
    CONSTRAINT [CK_DiscussionQuestion_Status] CHECK ([Status] IN ('Active', 'Hidden', 'Deleted'))
);
GO

-- Create Table: DiscussionReport
CREATE TABLE [DiscussionReport] (
    [ReportID] VARCHAR(36) NOT NULL,
    [DiscussionQuestionID] VARCHAR(36) NULL,
    [DiscussionAnswerID] VARCHAR(36) NULL,
    [ReporterAccountID] VARCHAR(36) NOT NULL,
    [ReportReason] NVARCHAR(MAX) NOT NULL,
    [Status] VARCHAR(20) NOT NULL DEFAULT 'Pending',
    [CreatedTime] DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ResolvedTime] DATETIME2(0) NULL,
    [ResolvedByAccountID] VARCHAR(36) NULL,
    CONSTRAINT [PK_DiscussionReport] PRIMARY KEY ([ReportID]),
    CONSTRAINT [CK_DiscussionReport_Target] CHECK (
        ([DiscussionQuestionID] IS NOT NULL AND [DiscussionAnswerID] IS NULL)
        OR
        ([DiscussionQuestionID] IS NULL AND [DiscussionAnswerID] IS NOT NULL)
    ),
    CONSTRAINT [CK_DiscussionReport_Status] CHECK ([Status] IN ('Pending', 'Resolved', 'Dismissed'))
);
GO

-- Create Table: Expert
CREATE TABLE [Expert] (
    [ExpertID] VARCHAR(36) NOT NULL,
    [Specialty] VARCHAR(100) NULL,
    CONSTRAINT [PK_Expert] PRIMARY KEY ([ExpertID])
);
GO

-- Create Table: Lecture
CREATE TABLE [Lecture] (
    [LectureID] VARCHAR(36) NOT NULL,
    [Title] NVARCHAR(100) NOT NULL,
    [Content] NVARCHAR(MAX) NULL,
    [VideoUrl] VARCHAR(255) NULL,
    [ThumbnailUrl] VARCHAR(255) NULL,
    [Likes] INT NOT NULL DEFAULT 0,
    [TeacherID] VARCHAR(36) NOT NULL,
    [TagID] VARCHAR(36) NOT NULL,
    [Status] VARCHAR(20) NOT NULL DEFAULT 'Draft',
    [CreatedTime] DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    [UpdatedTime] DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_Lecture] PRIMARY KEY ([LectureID]),
    CONSTRAINT [CK_Lecture_Status] CHECK ([Status] IN ('Draft', 'Published', 'Deactivated')),
    CONSTRAINT [CK_Lecture_Likes] CHECK ([Likes] >= 0)
);
GO

-- Create Table: Material
CREATE TABLE [Material] (
    [MaterialID] VARCHAR(36) NOT NULL,
    [MaterialName] NVARCHAR(100) NOT NULL,
    [FileUrl] VARCHAR(255) NOT NULL,
    [FileType] VARCHAR(10) NOT NULL,
    [TeacherID] VARCHAR(36) NOT NULL,
    [Status] VARCHAR(20) NOT NULL DEFAULT 'Active',
    [UploadedTime] DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_Material] PRIMARY KEY ([MaterialID]),
    CONSTRAINT [CK_Material_Status] CHECK ([Status] IN ('Active', 'Deactivated'))
);
GO

-- Create Table: Notification
CREATE TABLE [Notification] (
    [NotificationID] VARCHAR(36) NOT NULL,
    [UserID] VARCHAR(36) NOT NULL,
    [Title] NVARCHAR(100) NOT NULL,
    [Content] NVARCHAR(255) NOT NULL,
    [Link] VARCHAR(255) NULL,
    [isRead] BIT NOT NULL DEFAULT 0,
    [CreatedTime] DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_Notification] PRIMARY KEY ([NotificationID])
);
GO

-- Create Table: Permission
CREATE TABLE [Permission] (
    [PermissionID] VARCHAR(36) NOT NULL,
    [PermissionKey] VARCHAR(100) NOT NULL UNIQUE,
    [Description] NVARCHAR(255) NULL,
    CONSTRAINT [PK_Permission] PRIMARY KEY ([PermissionID])
);
GO

-- Create Table: Question
CREATE TABLE [Question] (
    [QuestionID] VARCHAR(36) NOT NULL,
    [QuestionContent] NVARCHAR(MAX) NOT NULL,
    [SolutionContent] NVARCHAR(MAX) NOT NULL,
    [PictureUrl] VARCHAR(255) NULL,
    [DifficultyID] VARCHAR(36) NOT NULL,
    [Grade] INT NOT NULL DEFAULT 10,
    [Status] VARCHAR(20) NOT NULL DEFAULT 'Approved',
    [QuestionType] VARCHAR(30) NOT NULL DEFAULT 'SingleChoice',
    [ExpertID] VARCHAR(36) NOT NULL,
    [DefaultPoint] DECIMAL(4,2) NOT NULL DEFAULT 0.20,
    [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_Question] PRIMARY KEY ([QuestionID]),
    CONSTRAINT [CK_Question_Grade] CHECK ([Grade] IN (10, 11, 12)),
    CONSTRAINT [CK_Question_Status] CHECK ([Status] IN ('Approved', 'Reported', 'Rejected', 'Deactivated')),
    CONSTRAINT [CK_Question_QuestionType] CHECK ([QuestionType] IN ('SingleChoice', 'MultipleChoice', 'TrueFalse', 'ShortAnswer', 'Composite')),
    CONSTRAINT [CK_Question_DefaultPoint] CHECK ([DefaultPoint] >= 0 AND [DefaultPoint] <= 10)
);
GO

-- Create Table: QuestionReport
CREATE TABLE [QuestionReport] (
    [ReportID] VARCHAR(36) NOT NULL,
    [QuestionID] VARCHAR(36) NOT NULL,
    [ReporterAccountID] VARCHAR(36) NOT NULL,
    [ReporterRole] VARCHAR(20) NOT NULL,
    [ReportReason] NVARCHAR(MAX) NOT NULL,
    [Status] VARCHAR(20) NOT NULL DEFAULT 'Pending',
    [CreatedTime] DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ResolvedTime] DATETIME2(0) NULL,
    [ResolvedBy] VARCHAR(36) NULL,
    [ReviewNote] NVARCHAR(2000) NULL,
    [SubmittedTime] DATETIME2(0) NULL,
    [ReviewedTime] DATETIME2(0) NULL,
    [ReviewedBy] VARCHAR(36) NULL,
    CONSTRAINT [PK_QuestionReport] PRIMARY KEY ([ReportID]),
    CONSTRAINT [CK_QuestionReport_ReporterRole] CHECK ([ReporterRole] IN ('Student', 'Expert', 'Admin')),
    CONSTRAINT [CK_QuestionReport_Status] CHECK ([Status] IN ('Pending', 'PendingFix', 'PendingReview', 'Resolved', 'Dismissed'))
);
GO

-- Create Table: QuestionTopic
CREATE TABLE [QuestionTopic] (
    [QuestionTopicID] VARCHAR(36) NOT NULL,
    [QuestionID] VARCHAR(36) NOT NULL,
    [TagID] VARCHAR(36) NOT NULL,
    [IsPrimary] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_QuestionTopic] PRIMARY KEY ([QuestionTopicID]),
    CONSTRAINT [UQ_QuestionTopic_Question_Tag] UNIQUE ([QuestionID], [TagID])
);
GO

-- Create Table: QuestionPart
CREATE TABLE [QuestionPart] (
    [PartID] VARCHAR(36) NOT NULL,
    [QuestionID] VARCHAR(36) NOT NULL,
    [PartOrder] INT NOT NULL,
    [PartLabel] NVARCHAR(10) NULL,
    [PartContent] NVARCHAR(MAX) NOT NULL,
    [PartType] VARCHAR(30) NOT NULL DEFAULT 'TrueFalse',
    [CorrectBoolean] BIT NULL,
    [CorrectText] NVARCHAR(255) NULL,
    [CorrectNumeric] DECIMAL(18,6) NULL,
    [NumericTolerance] DECIMAL(18,6) NULL,
    [Explanation] NVARCHAR(MAX) NULL,
    [DefaultPoint] DECIMAL(4,2) NOT NULL DEFAULT 0.00,
    CONSTRAINT [PK_QuestionPart] PRIMARY KEY ([PartID]),
    CONSTRAINT [UQ_QuestionPart_Question_Order] UNIQUE ([QuestionID], [PartOrder]),
    CONSTRAINT [CK_QuestionPart_Order] CHECK ([PartOrder] > 0),
    CONSTRAINT [CK_QuestionPart_Type] CHECK ([PartType] IN ('TrueFalse', 'ShortAnswer', 'NumericAnswer')),
    CONSTRAINT [CK_QuestionPart_DefaultPoint] CHECK ([DefaultPoint] >= 0 AND [DefaultPoint] <= 10),
    CONSTRAINT [CK_QuestionPart_NumericTolerance] CHECK ([NumericTolerance] IS NULL OR [NumericTolerance] >= 0),
    CONSTRAINT [CK_QuestionPart_CorrectAnswer_ByType] CHECK (
        (
            [PartType] = 'TrueFalse'
            AND [CorrectBoolean] IS NOT NULL
            AND [CorrectText] IS NULL
            AND [CorrectNumeric] IS NULL
            AND [NumericTolerance] IS NULL
        )
        OR
        (
            [PartType] = 'ShortAnswer'
            AND [CorrectBoolean] IS NULL
            AND [CorrectText] IS NOT NULL
            AND [CorrectNumeric] IS NULL
            AND [NumericTolerance] IS NULL
        )
        OR
        (
            [PartType] = 'NumericAnswer'
            AND [CorrectBoolean] IS NULL
            AND [CorrectText] IS NULL
            AND [CorrectNumeric] IS NOT NULL
        )
    )
);
GO

-- Create Table: QuestionVersion
CREATE TABLE [QuestionVersion] (
    [VersionID] VARCHAR(36) NOT NULL,
    [QuestionID] VARCHAR(36) NOT NULL,
    [QuestionContent] NVARCHAR(MAX) NOT NULL,
    [QuestionAnswer] NVARCHAR(MAX) NOT NULL,
    [AnswersSnapshot] NVARCHAR(MAX) NOT NULL,
    [PictureUrl] VARCHAR(255) NULL,
    [CreatedTime] DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ExpertID] VARCHAR(36) NOT NULL,
    CONSTRAINT [PK_QuestionVersion] PRIMARY KEY ([VersionID])
);
GO

-- Create Table: Role
CREATE TABLE [Role] (
    [RoleID] VARCHAR(36) NOT NULL,
    [RoleName] NVARCHAR(50) NOT NULL UNIQUE,
    [Description] NVARCHAR(255) NULL,
    CONSTRAINT [PK_Role] PRIMARY KEY ([RoleID])
);
GO

-- Create Table: RolePermission
CREATE TABLE [RolePermission] (
    [RoleID] VARCHAR(36) NOT NULL,
    [PermissionID] VARCHAR(36) NOT NULL,
    CONSTRAINT [PK_RolePermission] PRIMARY KEY ([RoleID], [PermissionID])
);
GO

-- Create Table: Student
CREATE TABLE [Student] (
    [StudentID] VARCHAR(36) NOT NULL,
    [Gender] VARCHAR(10) NULL,
    [School] NVARCHAR(100) NULL,
    [CurrentGrade] INT NULL,
    CONSTRAINT [PK_Student] PRIMARY KEY ([StudentID]),
    CONSTRAINT [CK_Student_CurrentGrade] CHECK ([CurrentGrade] IS NULL OR [CurrentGrade] IN (10, 11, 12))
);
GO

-- Create Table: StudentBadge
CREATE TABLE [StudentBadge] (
    [StudentID] VARCHAR(36) NOT NULL,
    [BadgeID] VARCHAR(36) NOT NULL,
    [EarnedTime] DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_StudentBadge] PRIMARY KEY ([StudentID], [BadgeID])
);
GO

-- Create Table: StudyStreak
CREATE TABLE [StudyStreak] (
    [StreakID] VARCHAR(36) NOT NULL,
    [StudentID] VARCHAR(36) NOT NULL UNIQUE,
    [CurrentStreak] INT NOT NULL DEFAULT 0,
    [LongestStreak] INT NOT NULL DEFAULT 0,
    [LastActivityDate] DATE NULL,
    CONSTRAINT [PK_StudyStreak] PRIMARY KEY ([StreakID]),
    CONSTRAINT [CK_StudyStreak_Values] CHECK (
        [CurrentStreak] >= 0
        AND [LongestStreak] >= 0
        AND [CurrentStreak] <= [LongestStreak]
    )
);
GO

-- Create Table: TagDifficulty
CREATE TABLE [TagDifficulty] (
    [DifficultyID] VARCHAR(36) NOT NULL,
    [DifficultyName] NVARCHAR(50) NOT NULL UNIQUE,
    [Description] NVARCHAR(255) NULL,
    [LevelValue] INT NOT NULL DEFAULT 1,
    [DisplayOrder] INT NOT NULL DEFAULT 1,
    [IsActive] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_TagDifficulty] PRIMARY KEY ([DifficultyID]),
    CONSTRAINT [CK_TagDifficulty_LevelValue] CHECK ([LevelValue] > 0),
    CONSTRAINT [CK_TagDifficulty_DisplayOrder] CHECK ([DisplayOrder] > 0)
);
GO

-- Create Table: TagTopic
CREATE TABLE [TagTopic] (
    [TagID] VARCHAR(36) NOT NULL,
    [ParentTagID] VARCHAR(36) NULL,
    [TagName] NVARCHAR(50) NOT NULL UNIQUE,
    [Description] NVARCHAR(255) NULL,
    [Grade] INT NOT NULL DEFAULT 10,
    [IsActive] BIT NOT NULL DEFAULT 1,
    [DisplayOrder] INT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_TagTopic] PRIMARY KEY ([TagID]),
    CONSTRAINT [CK_TagTopic_Grade] CHECK ([Grade] IN (10, 11, 12)),
    CONSTRAINT [CK_TagTopic_DisplayOrder] CHECK ([DisplayOrder] > 0),
    CONSTRAINT [CK_TagTopic_NotSelfParent] CHECK ([ParentTagID] IS NULL OR [ParentTagID] <> [TagID])
);
GO

-- Create Table: TagsMastery
CREATE TABLE [TagsMastery] (
    [TagsMasteryID] VARCHAR(36) NOT NULL,
    [StudentID] VARCHAR(36) NOT NULL,
    [TagID] VARCHAR(36) NOT NULL,
    [OfficialPoint] DECIMAL(5,2) NOT NULL DEFAULT 5.00,
    [PracticePoint] DECIMAL(5,2) NOT NULL DEFAULT 5.00,
    [ExamAnchor] DECIMAL(5,2) NOT NULL DEFAULT 5.00,
    [ExamHistory] NVARCHAR(MAX) NULL,
    [SeriesAnswerCount] INT NOT NULL DEFAULT 0,
    [RecommendedDifficultyLevel] TINYINT NOT NULL DEFAULT 2,
    [MasteryStatus] VARCHAR(20) NOT NULL DEFAULT 'NotLearned',
    [NumberDone] INT NOT NULL DEFAULT 0,
    [NumCorrect] INT NOT NULL DEFAULT 0,
    [AccuracyRate] DECIMAL(5,2) NOT NULL DEFAULT 0.00,
    [LastCalculatedAt] DATETIME2(0) NULL,
    [LastPracticedTime] DATETIME2(0) NULL,
    CONSTRAINT [PK_TagsMastery] PRIMARY KEY ([TagsMasteryID]),
    CONSTRAINT [UQ_TagsMastery_Student_Tag] UNIQUE ([StudentID], [TagID]),
    CONSTRAINT [CK_TagsMastery_Status] CHECK ([MasteryStatus] IN ('NotLearned', 'Learning', 'Mastered')),
    CONSTRAINT [CK_TagsMastery_Points] CHECK (
        [OfficialPoint] >= 0 AND [OfficialPoint] <= 10
        AND [PracticePoint] >= 0 AND [PracticePoint] <= 10
        AND [ExamAnchor] >= 0 AND [ExamAnchor] <= 10
    ),
    CONSTRAINT [CK_TagsMastery_ExamHistoryJson] CHECK ([ExamHistory] IS NULL OR ISJSON([ExamHistory]) = 1),
    CONSTRAINT [CK_TagsMastery_SeriesAnswerCount] CHECK ([SeriesAnswerCount] >= 0),
    CONSTRAINT [CK_TagsMastery_RecommendedDifficultyLevel] CHECK ([RecommendedDifficultyLevel] BETWEEN 1 AND 4),
    CONSTRAINT [CK_TagsMastery_Progress] CHECK (
        [NumberDone] >= 0
        AND [NumCorrect] >= 0
        AND [NumCorrect] <= [NumberDone]
        AND [AccuracyRate] >= 0
        AND [AccuracyRate] <= 100
    )
);
GO

-- Create Table: TargetScore
CREATE TABLE [TargetScore] (
    [TargetID] VARCHAR(36) NOT NULL,
    [StudentID] VARCHAR(36) NOT NULL,
    [TagID] VARCHAR(36) NOT NULL,
    [TargetPoint] DECIMAL(4,2) NOT NULL,
    [CreatedTime] DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    [UpdatedTime] DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_TargetScore] PRIMARY KEY ([TargetID]),
    CONSTRAINT [UQ_TargetScore_Student_Tag] UNIQUE ([StudentID], [TagID]),
    CONSTRAINT [CK_TargetScore_TargetPoint] CHECK ([TargetPoint] >= 0 AND [TargetPoint] <= 10)
);
GO

-- Create Table: Teacher
CREATE TABLE [Teacher] (
    [TeacherID] VARCHAR(36) NOT NULL,
    [Biography] NVARCHAR(MAX) NULL,
    [isVerified] BIT NOT NULL DEFAULT 0,
    [cccd_number] VARCHAR(12) NULL,
    CONSTRAINT [PK_Teacher] PRIMARY KEY ([TeacherID])
);
GO

-- Create Table: TeacherApplication
CREATE TABLE [TeacherApplication] (
    [ApplicationID] VARCHAR(36) NOT NULL,
    [TeacherID] VARCHAR(36) NOT NULL,
    [DocumentsUrl] VARCHAR(255) NOT NULL,
    [Status] VARCHAR(20) NOT NULL DEFAULT 'Pending',
    [ReviewComments] NVARCHAR(255) NULL,
    [AppliedTime] DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    [ReviewedTime] DATETIME2(0) NULL,
    [ReviewedBy] VARCHAR(36) NULL,
    CONSTRAINT [PK_TeacherApplication] PRIMARY KEY ([ApplicationID]),
    CONSTRAINT [CK_TeacherApplication_Status] CHECK ([Status] IN ('Pending', 'Approved', 'Rejected')),
    CONSTRAINT [CK_TeacherApplication_Review] CHECK (
        ([Status] = 'Pending' AND [ReviewedTime] IS NULL AND [ReviewedBy] IS NULL)
        OR
        ([Status] IN ('Approved', 'Rejected') AND [ReviewedTime] IS NOT NULL AND [ReviewedBy] IS NOT NULL)
    )
);
GO

-- Create Table: Test
CREATE TABLE [Test] (
    [TestID] VARCHAR(36) NOT NULL,
    [BlueprintID] VARCHAR(36) NULL,
    [TestStatus] VARCHAR(20) NOT NULL DEFAULT 'Active',
    [TestMode] VARCHAR(30) NOT NULL DEFAULT 'BlueprintExam',
    [GeneratedForStudentID] VARCHAR(36) NULL,
    [GeneratedBy] VARCHAR(20) NOT NULL DEFAULT 'System',
    [TestName] NVARCHAR(100) NOT NULL,
    [TestCode] VARCHAR(20) NULL,
    [DurationMinutes] INT NOT NULL,
    [TotalQuestions] INT NOT NULL,
    [CreatedTime] DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_Test] PRIMARY KEY ([TestID]),
    CONSTRAINT [CK_Test_Status] CHECK ([TestStatus] IN ('Active', 'Archived')),
    CONSTRAINT [CK_Test_Mode] CHECK ([TestMode] IN ('BlueprintExam', 'AdaptivePractice', 'TopicPractice', 'Diagnostic')),
    CONSTRAINT [CK_Test_GeneratedBy] CHECK ([GeneratedBy] IN ('Expert', 'System')),
    CONSTRAINT [CK_Test_Blueprint_Required] CHECK ([TestMode] <> 'BlueprintExam' OR [BlueprintID] IS NOT NULL),
    CONSTRAINT [CK_Test_DurationMinutes] CHECK ([DurationMinutes] > 0),
    CONSTRAINT [CK_Test_TotalQuestions] CHECK ([TotalQuestions] > 0)
);
GO

-- Create Table: TestAnswer
CREATE TABLE [TestAnswer] (
    [TestAnswerID] VARCHAR(36) NOT NULL,
    [SessionID] VARCHAR(36) NOT NULL,
    [QuestionID] VARCHAR(36) NOT NULL,
    [AnswerID] VARCHAR(36) NULL,
    [QuestionNo] INT NOT NULL,
    [TimeSpent] INT NULL DEFAULT 0,
    [FirstChoiceTime] DATETIME2(0) NULL,
    [UpdateChoiceTime] DATETIME2(0) NULL,
    [ShortAnswerText] NVARCHAR(MAX) NULL,
    [IsCorrect] BIT NULL,
    [PointsEarned] DECIMAL(4,2) NOT NULL DEFAULT 0.00,
    CONSTRAINT [PK_TestAnswer] PRIMARY KEY ([TestAnswerID]),
    CONSTRAINT [UQ_TestAnswer_Session_Question] UNIQUE ([SessionID], [QuestionID]),
    CONSTRAINT [CK_TestAnswer_QuestionNo] CHECK ([QuestionNo] > 0),
    CONSTRAINT [CK_TestAnswer_TimeSpent] CHECK ([TimeSpent] IS NULL OR [TimeSpent] >= 0),
    CONSTRAINT [CK_TestAnswer_PointsEarned] CHECK ([PointsEarned] >= 0 AND [PointsEarned] <= 10)
);
GO

-- Create Table: TestAnswerOption
CREATE TABLE [TestAnswerOption] (
    [TestAnswerID] VARCHAR(36) NOT NULL,
    [AnswerID] VARCHAR(36) NOT NULL,
    CONSTRAINT [PK_TestAnswerOption] PRIMARY KEY ([TestAnswerID], [AnswerID])
);
GO

-- Create Table: TestAnswerPart
CREATE TABLE [TestAnswerPart] (
    [TestAnswerID] VARCHAR(36) NOT NULL,
    [PartID] VARCHAR(36) NOT NULL,
    [BooleanAnswer] BIT NULL,
    [TextAnswer] NVARCHAR(255) NULL,
    [NumericAnswer] DECIMAL(18,6) NULL,
    [IsCorrect] BIT NULL,
    [PointsEarned] DECIMAL(4,2) NOT NULL DEFAULT 0.00,
    CONSTRAINT [PK_TestAnswerPart] PRIMARY KEY ([TestAnswerID], [PartID]),
    CONSTRAINT [CK_TestAnswerPart_PointsEarned] CHECK ([PointsEarned] >= 0 AND [PointsEarned] <= 10)
);
GO

-- Create Table: TestIncidents
CREATE TABLE [TestIncidents] (
    [IncidentID] VARCHAR(36) NOT NULL,
    [SessionID] VARCHAR(36) NOT NULL,
    [Type] VARCHAR(50) NOT NULL,
    [Time] DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_TestIncidents] PRIMARY KEY ([IncidentID])
);
GO

-- Create Table: TestQuestion
CREATE TABLE [TestQuestion] (
    [TestID] VARCHAR(36) NOT NULL,
    [QuestionID] VARCHAR(36) NOT NULL,
    [QuestionOrder] INT NOT NULL DEFAULT 1,
    [SourceBlueprintDetailID] VARCHAR(36) NULL,
    [SelectionReason] VARCHAR(40) NOT NULL DEFAULT 'BlueprintNormal',
    [IsAdaptiveSelected] BIT NOT NULL DEFAULT 0,
    [RecommendedForTagID] VARCHAR(36) NULL,
    [RecommendedDifficultyID] VARCHAR(36) NULL,
    [PtagAtSelection] DECIMAL(5,2) NULL,
    [RuleVersion] VARCHAR(30) NULL,
    CONSTRAINT [PK_TestQuestion] PRIMARY KEY ([TestID], [QuestionID]),
    CONSTRAINT [UQ_TestQuestion_Test_Order] UNIQUE ([TestID], [QuestionOrder]),
    CONSTRAINT [CK_TestQuestion_QuestionOrder] CHECK ([QuestionOrder] > 0),
    CONSTRAINT [CK_TestQuestion_PtagAtSelection] CHECK ([PtagAtSelection] IS NULL OR ([PtagAtSelection] >= 0 AND [PtagAtSelection] <= 10)),
    CONSTRAINT [CK_TestQuestion_SelectionReason] CHECK ([SelectionReason] IN (
        'BlueprintNormal',
        'WeakTagPractice',
        'RemedialPractice',
        'ChallengeMode',
        'Exploration',
        'TopicPractice',
        'Diagnostic'
    ))
);
GO

-- Create Table: TestSession
CREATE TABLE [TestSession] (
    [SessionID] VARCHAR(36) NOT NULL,
    [TestID] VARCHAR(36) NOT NULL,
    [StudentID] VARCHAR(36) NOT NULL,
    [TestFormat] VARCHAR(20) NOT NULL,
    [Status] VARCHAR(20) NOT NULL DEFAULT 'InProgress',
    [SubmissionType] VARCHAR(30) NULL,
    [Duration] INT NOT NULL DEFAULT 0,
    [StartTime] DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    [EndTime] DATETIME2(0) NULL,
    [TotalQuestion] INT NOT NULL DEFAULT 0,
    [NumCorrect] INT NOT NULL DEFAULT 0,
    [NumIncorrect] INT NOT NULL DEFAULT 0,
    [NumAbandoned] INT NOT NULL DEFAULT 0,
    [Score] DECIMAL(5,2) NOT NULL DEFAULT 0.00,
    CONSTRAINT [PK_TestSession] PRIMARY KEY ([SessionID]),
    CONSTRAINT [CK_TestSession_Status] CHECK ([Status] IN ('InProgress', 'Graded', 'Abandoned')),
    CONSTRAINT [CK_TestSession_SubmissionType] CHECK ([SubmissionType] IS NULL OR [SubmissionType] IN ('StudentSubmit', 'TimeoutSubmit', 'SystemSubmit')),
    CONSTRAINT [CK_TestSession_SubmissionType_Status] CHECK (
        ([Status] = 'Graded' AND [SubmissionType] IS NOT NULL)
        OR ([Status] IN ('InProgress', 'Abandoned') AND [SubmissionType] IS NULL)
    ),
    CONSTRAINT [CK_TestSession_TestFormat] CHECK ([TestFormat] IN ('Practice', 'Exam', 'PRACTICE', 'EXAM')),
    CONSTRAINT [CK_TestSession_Duration] CHECK ([Duration] >= 0),
    CONSTRAINT [CK_TestSession_Counts] CHECK (
        [TotalQuestion] >= 0
        AND [NumCorrect] >= 0
        AND [NumIncorrect] >= 0
        AND [NumAbandoned] >= 0
        AND [NumCorrect] + [NumIncorrect] + [NumAbandoned] <= [TotalQuestion]
    ),
    CONSTRAINT [CK_TestSession_Score] CHECK ([Score] >= 0 AND [Score] <= 10),
    CONSTRAINT [CK_TestSession_Time] CHECK ([EndTime] IS NULL OR [EndTime] >= [StartTime])
);
GO

-- Create Table: LectureMaterial
CREATE TABLE [LectureMaterial] (
    [LectureID] VARCHAR(36) NOT NULL,
    [MaterialID] VARCHAR(36) NOT NULL,
    CONSTRAINT [PK_LectureMaterial] PRIMARY KEY ([LectureID], [MaterialID])
);
GO

-- Create Table: StudentTopicSessionResult
CREATE TABLE [StudentTopicSessionResult] (
    [StudentTopicSessionResultID] VARCHAR(36) NOT NULL,
    [StudentID] VARCHAR(36) NOT NULL,
    [SessionID] VARCHAR(36) NOT NULL,
    [TagID] VARCHAR(36) NOT NULL,
    [TotalItems] DECIMAL(6,2) NOT NULL DEFAULT 0.00,
    [CorrectItems] DECIMAL(6,2) NOT NULL DEFAULT 0.00,
    [EarnedPoints] DECIMAL(6,2) NOT NULL DEFAULT 0.00,
    [MaxPoints] DECIMAL(6,2) NOT NULL DEFAULT 0.00,
    [TopicScore] DECIMAL(5,2) NOT NULL DEFAULT 0.00,
    [CreatedTime] DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_StudentTopicSessionResult] PRIMARY KEY ([StudentTopicSessionResultID]),
    CONSTRAINT [UQ_StudentTopicSessionResult_Session_Tag] UNIQUE ([SessionID], [TagID]),
    CONSTRAINT [CK_StudentTopicSessionResult_Values] CHECK (
        [TotalItems] >= 0
        AND [CorrectItems] >= 0
        AND [CorrectItems] <= [TotalItems]
        AND [EarnedPoints] >= 0
        AND [MaxPoints] >= 0
        AND [EarnedPoints] <= [MaxPoints]
        AND [TopicScore] >= 0
        AND [TopicScore] <= 10
    )
);
GO

-- Create Table: LectureLike
CREATE TABLE [LectureLike] (
    [LectureID] VARCHAR(36) NOT NULL,
    [StudentID] VARCHAR(36) NOT NULL,
    [CreatedTime] DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_LectureLike] PRIMARY KEY ([LectureID], [StudentID])
);
GO

-- ==========================================================
-- Add Foreign Key Constraints
-- ==========================================================

ALTER TABLE [Account] ADD CONSTRAINT [FK_Account_Role_RoleID] FOREIGN KEY ([RoleID]) REFERENCES [Role] ([RoleID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [ActivityLog] ADD CONSTRAINT [FK_ActivityLog_Student_StudentID] FOREIGN KEY ([StudentID]) REFERENCES [Student] ([StudentID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [ActivityLog] ADD CONSTRAINT [FK_ActivityLog_TestSession_TestSessionID] FOREIGN KEY ([TestSessionID]) REFERENCES [TestSession] ([SessionID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [ActivityLog] ADD CONSTRAINT [FK_ActivityLog_Lecture_LectureID] FOREIGN KEY ([LectureID]) REFERENCES [Lecture] ([LectureID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [ActivityLog] ADD CONSTRAINT [FK_ActivityLog_Material_MaterialID] FOREIGN KEY ([MaterialID]) REFERENCES [Material] ([MaterialID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [Answer] ADD CONSTRAINT [FK_Answer_Question_QuestionID] FOREIGN KEY ([QuestionID]) REFERENCES [Question] ([QuestionID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [Blueprint] ADD CONSTRAINT [FK_Blueprint_Expert_ExpertID] FOREIGN KEY ([ExpertID]) REFERENCES [Expert] ([ExpertID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [Blueprint] ADD CONSTRAINT [FK_Blueprint_Expert_ApprovedBy] FOREIGN KEY ([ApprovedBy]) REFERENCES [Expert] ([ExpertID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [BlueprintSection] ADD CONSTRAINT [FK_BlueprintSection_Blueprint_BlueprintID] FOREIGN KEY ([BlueprintID]) REFERENCES [Blueprint] ([BlueprintID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [BlueprintDetail] ADD CONSTRAINT [FK_BlueprintDetail_BlueprintSection_BlueprintSectionID] FOREIGN KEY ([BlueprintSectionID], [BlueprintID]) REFERENCES [BlueprintSection] ([BlueprintSectionID], [BlueprintID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [BlueprintDetail] ADD CONSTRAINT [FK_BlueprintDetail_TagTopic_TagID] FOREIGN KEY ([TagID]) REFERENCES [TagTopic] ([TagID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [BlueprintDetail] ADD CONSTRAINT [FK_BlueprintDetail_TagDifficulty_DifficultyID] FOREIGN KEY ([DifficultyID]) REFERENCES [TagDifficulty] ([DifficultyID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [CompetencyPoint] ADD CONSTRAINT [FK_CompetencyPoint_Student_StudentID] FOREIGN KEY ([StudentID]) REFERENCES [Student] ([StudentID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [DiscussionAnswer] ADD CONSTRAINT [FK_DiscussionAnswer_DiscussionQuestion_DiscussionQuestionID] FOREIGN KEY ([DiscussionQuestionID]) REFERENCES [DiscussionQuestion] ([DiscussionQuestionID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [DiscussionAnswer] ADD CONSTRAINT [FK_DiscussionAnswer_Account_AccountID] FOREIGN KEY ([AccountID]) REFERENCES [Account] ([AccountID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [DiscussionQuestion] ADD CONSTRAINT [FK_DiscussionQuestion_Lecture_LectureID] FOREIGN KEY ([LectureID]) REFERENCES [Lecture] ([LectureID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [DiscussionQuestion] ADD CONSTRAINT [FK_DiscussionQuestion_Student_StudentID] FOREIGN KEY ([StudentID]) REFERENCES [Student] ([StudentID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [DiscussionReport] ADD CONSTRAINT [FK_DiscussionReport_DiscussionQuestion_DiscussionQuestionID] FOREIGN KEY ([DiscussionQuestionID]) REFERENCES [DiscussionQuestion] ([DiscussionQuestionID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [DiscussionReport] ADD CONSTRAINT [FK_DiscussionReport_DiscussionAnswer_DiscussionAnswerID] FOREIGN KEY ([DiscussionAnswerID]) REFERENCES [DiscussionAnswer] ([DiscussionAnswerID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [DiscussionReport] ADD CONSTRAINT [FK_DiscussionReport_Account_ReporterAccountID] FOREIGN KEY ([ReporterAccountID]) REFERENCES [Account] ([AccountID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [DiscussionReport] ADD CONSTRAINT [FK_DiscussionReport_Account_ResolvedByAccountID] FOREIGN KEY ([ResolvedByAccountID]) REFERENCES [Account] ([AccountID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [Expert] ADD CONSTRAINT [FK_Expert_Account_ExpertID] FOREIGN KEY ([ExpertID]) REFERENCES [Account] ([AccountID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [Lecture] ADD CONSTRAINT [FK_Lecture_Teacher_TeacherID] FOREIGN KEY ([TeacherID]) REFERENCES [Teacher] ([TeacherID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [Lecture] ADD CONSTRAINT [FK_Lecture_TagTopic_TagID] FOREIGN KEY ([TagID]) REFERENCES [TagTopic] ([TagID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [Material] ADD CONSTRAINT [FK_Material_Teacher_TeacherID] FOREIGN KEY ([TeacherID]) REFERENCES [Teacher] ([TeacherID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [Notification] ADD CONSTRAINT [FK_Notification_Account_UserID] FOREIGN KEY ([UserID]) REFERENCES [Account] ([AccountID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [Question] ADD CONSTRAINT [FK_Question_TagDifficulty_DifficultyID] FOREIGN KEY ([DifficultyID]) REFERENCES [TagDifficulty] ([DifficultyID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [Question] ADD CONSTRAINT [FK_Question_Expert_ExpertID] FOREIGN KEY ([ExpertID]) REFERENCES [Expert] ([ExpertID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [QuestionReport] ADD CONSTRAINT [FK_QuestionReport_Question_QuestionID] FOREIGN KEY ([QuestionID]) REFERENCES [Question] ([QuestionID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [QuestionReport] ADD CONSTRAINT [FK_QuestionReport_Account_ReporterAccountID] FOREIGN KEY ([ReporterAccountID]) REFERENCES [Account] ([AccountID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [QuestionReport] ADD CONSTRAINT [FK_QuestionReport_Account_ResolvedBy] FOREIGN KEY ([ResolvedBy]) REFERENCES [Account] ([AccountID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [QuestionReport] ADD CONSTRAINT [FK_QuestionReport_Account_ReviewedBy] FOREIGN KEY ([ReviewedBy]) REFERENCES [Account] ([AccountID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [QuestionTopic] ADD CONSTRAINT [FK_QuestionTopic_Question_QuestionID] FOREIGN KEY ([QuestionID]) REFERENCES [Question] ([QuestionID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [QuestionTopic] ADD CONSTRAINT [FK_QuestionTopic_TagTopic_TagID] FOREIGN KEY ([TagID]) REFERENCES [TagTopic] ([TagID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [QuestionPart] ADD CONSTRAINT [FK_QuestionPart_Question_QuestionID] FOREIGN KEY ([QuestionID]) REFERENCES [Question] ([QuestionID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [QuestionVersion] ADD CONSTRAINT [FK_QuestionVersion_Question_QuestionID] FOREIGN KEY ([QuestionID]) REFERENCES [Question] ([QuestionID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [QuestionVersion] ADD CONSTRAINT [FK_QuestionVersion_Expert_ExpertID] FOREIGN KEY ([ExpertID]) REFERENCES [Expert] ([ExpertID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [RolePermission] ADD CONSTRAINT [FK_RolePermission_Role_RoleID] FOREIGN KEY ([RoleID]) REFERENCES [Role] ([RoleID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [RolePermission] ADD CONSTRAINT [FK_RolePermission_Permission_PermissionID] FOREIGN KEY ([PermissionID]) REFERENCES [Permission] ([PermissionID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [Student] ADD CONSTRAINT [FK_Student_Account_StudentID] FOREIGN KEY ([StudentID]) REFERENCES [Account] ([AccountID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [StudentBadge] ADD CONSTRAINT [FK_StudentBadge_Student_StudentID] FOREIGN KEY ([StudentID]) REFERENCES [Student] ([StudentID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [StudentBadge] ADD CONSTRAINT [FK_StudentBadge_Badge_BadgeID] FOREIGN KEY ([BadgeID]) REFERENCES [Badge] ([BadgeID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [StudyStreak] ADD CONSTRAINT [FK_StudyStreak_Student_StudentID] FOREIGN KEY ([StudentID]) REFERENCES [Student] ([StudentID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [TagTopic] ADD CONSTRAINT [FK_TagTopic_TagTopic_ParentTagID] FOREIGN KEY ([ParentTagID]) REFERENCES [TagTopic] ([TagID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [TagsMastery] ADD CONSTRAINT [FK_TagsMastery_Student_StudentID] FOREIGN KEY ([StudentID]) REFERENCES [Student] ([StudentID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [TagsMastery] ADD CONSTRAINT [FK_TagsMastery_TagTopic_TagID] FOREIGN KEY ([TagID]) REFERENCES [TagTopic] ([TagID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [TargetScore] ADD CONSTRAINT [FK_TargetScore_Student_StudentID] FOREIGN KEY ([StudentID]) REFERENCES [Student] ([StudentID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [TargetScore] ADD CONSTRAINT [FK_TargetScore_TagTopic_TagID] FOREIGN KEY ([TagID]) REFERENCES [TagTopic] ([TagID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [Teacher] ADD CONSTRAINT [FK_Teacher_Account_TeacherID] FOREIGN KEY ([TeacherID]) REFERENCES [Account] ([AccountID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [TeacherApplication] ADD CONSTRAINT [FK_TeacherApplication_Teacher_TeacherID] FOREIGN KEY ([TeacherID]) REFERENCES [Teacher] ([TeacherID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [TeacherApplication] ADD CONSTRAINT [FK_TeacherApplication_Account_ReviewedBy] FOREIGN KEY ([ReviewedBy]) REFERENCES [Account] ([AccountID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [Test] ADD CONSTRAINT [FK_Test_Blueprint_BlueprintID] FOREIGN KEY ([BlueprintID]) REFERENCES [Blueprint] ([BlueprintID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [Test] ADD CONSTRAINT [FK_Test_Student_GeneratedForStudentID] FOREIGN KEY ([GeneratedForStudentID]) REFERENCES [Student] ([StudentID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [TestAnswer] ADD CONSTRAINT [FK_TestAnswer_TestSession_SessionID] FOREIGN KEY ([SessionID]) REFERENCES [TestSession] ([SessionID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [TestAnswer] ADD CONSTRAINT [FK_TestAnswer_Question_QuestionID] FOREIGN KEY ([QuestionID]) REFERENCES [Question] ([QuestionID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [TestAnswer] ADD CONSTRAINT [FK_TestAnswer_Answer_AnswerID] FOREIGN KEY ([AnswerID]) REFERENCES [Answer] ([AnswerID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [TestAnswerOption] ADD CONSTRAINT [FK_TestAnswerOption_TestAnswer_TestAnswerID] FOREIGN KEY ([TestAnswerID]) REFERENCES [TestAnswer] ([TestAnswerID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [TestAnswerOption] ADD CONSTRAINT [FK_TestAnswerOption_Answer_AnswerID] FOREIGN KEY ([AnswerID]) REFERENCES [Answer] ([AnswerID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [TestAnswerPart] ADD CONSTRAINT [FK_TestAnswerPart_TestAnswer_TestAnswerID] FOREIGN KEY ([TestAnswerID]) REFERENCES [TestAnswer] ([TestAnswerID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [TestAnswerPart] ADD CONSTRAINT [FK_TestAnswerPart_QuestionPart_PartID] FOREIGN KEY ([PartID]) REFERENCES [QuestionPart] ([PartID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [TestIncidents] ADD CONSTRAINT [FK_TestIncidents_TestSession_SessionID] FOREIGN KEY ([SessionID]) REFERENCES [TestSession] ([SessionID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [TestQuestion] ADD CONSTRAINT [FK_TestQuestion_Test_TestID] FOREIGN KEY ([TestID]) REFERENCES [Test] ([TestID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [TestQuestion] ADD CONSTRAINT [FK_TestQuestion_Question_QuestionID] FOREIGN KEY ([QuestionID]) REFERENCES [Question] ([QuestionID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [TestQuestion] ADD CONSTRAINT [FK_TestQuestion_BlueprintDetail_SourceBlueprintDetailID] FOREIGN KEY ([SourceBlueprintDetailID]) REFERENCES [BlueprintDetail] ([BlueprintDetailID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [TestQuestion] ADD CONSTRAINT [FK_TestQuestion_TagTopic_RecommendedForTagID] FOREIGN KEY ([RecommendedForTagID]) REFERENCES [TagTopic] ([TagID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [TestQuestion] ADD CONSTRAINT [FK_TestQuestion_TagDifficulty_RecommendedDifficultyID] FOREIGN KEY ([RecommendedDifficultyID]) REFERENCES [TagDifficulty] ([DifficultyID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [TestSession] ADD CONSTRAINT [FK_TestSession_Test_TestID] FOREIGN KEY ([TestID]) REFERENCES [Test] ([TestID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [TestSession] ADD CONSTRAINT [FK_TestSession_Student_StudentID] FOREIGN KEY ([StudentID]) REFERENCES [Student] ([StudentID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [StudentTopicSessionResult] ADD CONSTRAINT [FK_StudentTopicSessionResult_Student_StudentID] FOREIGN KEY ([StudentID]) REFERENCES [Student] ([StudentID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [StudentTopicSessionResult] ADD CONSTRAINT [FK_StudentTopicSessionResult_TestSession_SessionID] FOREIGN KEY ([SessionID]) REFERENCES [TestSession] ([SessionID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [StudentTopicSessionResult] ADD CONSTRAINT [FK_StudentTopicSessionResult_TagTopic_TagID] FOREIGN KEY ([TagID]) REFERENCES [TagTopic] ([TagID]) ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE [LectureMaterial] ADD CONSTRAINT [FK_LectureMaterial_Lecture_LectureID] FOREIGN KEY ([LectureID]) REFERENCES [Lecture] ([LectureID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [LectureMaterial] ADD CONSTRAINT [FK_LectureMaterial_Material_MaterialID] FOREIGN KEY ([MaterialID]) REFERENCES [Material] ([MaterialID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [LectureLike] ADD CONSTRAINT [FK_LectureLike_Lecture_LectureID] FOREIGN KEY ([LectureID]) REFERENCES [Lecture] ([LectureID]) ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE [LectureLike] ADD CONSTRAINT [FK_LectureLike_Student_StudentID] FOREIGN KEY ([StudentID]) REFERENCES [Student] ([StudentID]) ON DELETE CASCADE ON UPDATE NO ACTION;
GO

-- ==========================================================
-- Unique filtered indexes for nullable business identifiers
-- ==========================================================

CREATE UNIQUE INDEX [UX_Account_PhoneNumber_NotNull]
    ON [Account] ([PhoneNumber])
    WHERE [PhoneNumber] IS NOT NULL;

CREATE UNIQUE INDEX [UX_Account_GoogleSubID_NotNull]
    ON [Account] ([GoogleSubID])
    WHERE [GoogleSubID] IS NOT NULL;

CREATE UNIQUE INDEX [UX_Teacher_CccdNumber_NotNull]
    ON [Teacher] ([cccd_number])
    WHERE [cccd_number] IS NOT NULL;

CREATE UNIQUE INDEX [UX_QuestionTopic_OnePrimaryPerQuestion]
    ON [QuestionTopic] ([QuestionID])
    WHERE [IsPrimary] = 1;

CREATE UNIQUE INDEX [UX_QuestionPart_Label_NotNull]
    ON [QuestionPart] ([QuestionID], [PartLabel])
    WHERE [PartLabel] IS NOT NULL;

CREATE UNIQUE INDEX [UX_Test_TestCode_NotNull]
    ON [Test] ([TestCode])
    WHERE [TestCode] IS NOT NULL;
GO

-- ==========================================================
-- Supporting indexes for common foreign-key and filter queries
-- ==========================================================

CREATE INDEX [IX_Account_RoleID] ON [Account] ([RoleID]);
CREATE INDEX [IX_ActivityLog_Student_ActivityDate] ON [ActivityLog] ([StudentID], [ActivityDate]);
CREATE INDEX [IX_ActivityLog_TestSessionID] ON [ActivityLog] ([TestSessionID]) WHERE [TestSessionID] IS NOT NULL;
CREATE INDEX [IX_Answer_QuestionID] ON [Answer] ([QuestionID]);
CREATE INDEX [IX_Blueprint_ExpertID] ON [Blueprint] ([ExpertID]);
CREATE INDEX [IX_BlueprintSection_BlueprintID] ON [BlueprintSection] ([BlueprintID]);
CREATE INDEX [IX_BlueprintDetail_BlueprintSectionID] ON [BlueprintDetail] ([BlueprintSectionID]);
CREATE INDEX [IX_DiscussionAnswer_DiscussionQuestionID] ON [DiscussionAnswer] ([DiscussionQuestionID]);
CREATE INDEX [IX_DiscussionQuestion_LectureID] ON [DiscussionQuestion] ([LectureID]);
CREATE INDEX [IX_DiscussionReport_Status] ON [DiscussionReport] ([Status]);
CREATE INDEX [IX_Lecture_Status_TagID] ON [Lecture] ([Status], [TagID]);
CREATE INDEX [IX_Lecture_TeacherID] ON [Lecture] ([TeacherID]);
CREATE INDEX [IX_LectureLike_StudentID] ON [LectureLike] ([StudentID]);
CREATE INDEX [IX_Material_TeacherID] ON [Material] ([TeacherID]);
CREATE INDEX [IX_Notification_User_IsRead] ON [Notification] ([UserID], [isRead]);
CREATE INDEX [IX_Question_Status_IsActive] ON [Question] ([Status], [IsActive]);
CREATE INDEX [IX_Question_ExpertID] ON [Question] ([ExpertID]);
CREATE INDEX [IX_QuestionPart_QuestionID] ON [QuestionPart] ([QuestionID]);
CREATE INDEX [IX_QuestionReport_Question_Status] ON [QuestionReport] ([QuestionID], [Status]);
CREATE INDEX [IX_QuestionReport_ReporterAccountID] ON [QuestionReport] ([ReporterAccountID]);
CREATE UNIQUE INDEX [UX_TagDifficulty_LevelValue] ON [TagDifficulty] ([LevelValue]);
CREATE INDEX [IX_QuestionVersion_QuestionID] ON [QuestionVersion] ([QuestionID]);
CREATE INDEX [IX_TeacherApplication_Teacher_Status] ON [TeacherApplication] ([TeacherID], [Status]);
CREATE INDEX [IX_Test_BlueprintID] ON [Test] ([BlueprintID]);
CREATE INDEX [IX_Test_Mode_GeneratedForStudent] ON [Test] ([TestMode], [GeneratedForStudentID]);
CREATE INDEX [IX_Test_GeneratedForStudent_CreatedTime] ON [Test] ([GeneratedForStudentID], [CreatedTime]) WHERE [GeneratedForStudentID] IS NOT NULL;
CREATE INDEX [IX_TagsMastery_Student_OfficialPoint] ON [TagsMastery] ([StudentID], [OfficialPoint], [TagID]);
CREATE INDEX [IX_TestQuestion_SelectionReason] ON [TestQuestion] ([SelectionReason]);
CREATE INDEX [IX_TestQuestion_RecommendedTag_Difficulty] ON [TestQuestion] ([RecommendedForTagID], [RecommendedDifficultyID]) WHERE [RecommendedForTagID] IS NOT NULL;
CREATE INDEX [IX_TestQuestion_SourceBlueprintDetailID] ON [TestQuestion] ([SourceBlueprintDetailID]) WHERE [SourceBlueprintDetailID] IS NOT NULL;
CREATE INDEX [IX_TestAnswerPart_PartID] ON [TestAnswerPart] ([PartID]);
CREATE INDEX [IX_TestSession_Student_StartTime] ON [TestSession] ([StudentID], [StartTime]);
CREATE INDEX [IX_TestSession_Student_Status] ON [TestSession] ([StudentID], [Status]);
CREATE INDEX [IX_TestSession_TestID] ON [TestSession] ([TestID]);
CREATE INDEX [IX_StudentTopicSessionResult_Student_Tag_Created] ON [StudentTopicSessionResult] ([StudentID], [TagID], [CreatedTime]);
CREATE INDEX [IX_StudentTopicSessionResult_SessionID] ON [StudentTopicSessionResult] ([SessionID]);
GO
