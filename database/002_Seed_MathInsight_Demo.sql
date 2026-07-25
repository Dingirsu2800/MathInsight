/*
==========================================================
MathInsight consolidated development/demo seed
Run after: 001_Create_MathInsight_Azure.sql

WARNING: Creates predictable development accounts whose password is "password".
Do not run this file against a production database.
The script is idempotent and may be run again on the same development database.

When using sqlcmd, disable variable substitution and force UTF-8 input:
    sqlcmd ... -x -f 65001 -i database/002_Seed_MathInsight_Demo.sql
==========================================================
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ===== SOURCE: MathInsight_Dev_Seed.sql ===== */
/*
    MathInsight development seed data for local login testing.

    All seeded accounts are active and use the same password:

        password

    This script is idempotent: running it again updates the same dev accounts
    instead of creating duplicates.
*/

SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
BEGIN TRANSACTION;

DECLARE @PasswordHash VARCHAR(255) = '$2a$11$IgmdnGcpWz7hvryLYzdjQ..DHZIv4jtsRxhDV8qVG7RixntcnJuRa';

DECLARE @AdminRoleId   VARCHAR(36) = '11111111-1111-1111-1111-111111111111';
DECLARE @ExpertRoleId  VARCHAR(36) = '22222222-2222-2222-2222-222222222222';
DECLARE @TeacherRoleId VARCHAR(36) = '33333333-3333-3333-3333-333333333333';
DECLARE @StudentRoleId VARCHAR(36) = '44444444-4444-4444-4444-444444444444';

MERGE [Role] AS target
USING (VALUES
    (@AdminRoleId,   N'Admin',   N'System administrator'),
    (@ExpertRoleId,  N'Expert',  N'Question bank expert'),
    (@TeacherRoleId, N'Teacher', N'Verified teacher'),
    (@StudentRoleId, N'Student', N'Student user')
) AS source ([RoleID], [RoleName], [Description])
ON target.[RoleName] = source.[RoleName]
WHEN MATCHED THEN
    UPDATE SET
        target.[Description] = source.[Description]
WHEN NOT MATCHED THEN
    INSERT ([RoleID], [RoleName], [Description])
    VALUES (source.[RoleID], source.[RoleName], source.[Description]);

SELECT @AdminRoleId = [RoleID] FROM [Role] WHERE [RoleName] = N'Admin';
SELECT @ExpertRoleId = [RoleID] FROM [Role] WHERE [RoleName] = N'Expert';
SELECT @TeacherRoleId = [RoleID] FROM [Role] WHERE [RoleName] = N'Teacher';
SELECT @StudentRoleId = [RoleID] FROM [Role] WHERE [RoleName] = N'Student';

MERGE [Account] AS target
USING (VALUES
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', N'admin',      @PasswordHash, 'admin@mathinsight.local',      N'Admin',   N'User',    @AdminRoleId,   CAST(1 AS BIT)),
    ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', N'expert_01',  @PasswordHash, 'expert01@mathinsight.local',   N'Expert',  N'One',     @ExpertRoleId,  CAST(1 AS BIT)),
    ('ffffffff-ffff-ffff-ffff-ffffffffffff', N'expert_02',  @PasswordHash, 'expert02@mathinsight.local',   N'Expert',  N'Two',     @ExpertRoleId,  CAST(1 AS BIT)),
    ('cccccccc-cccc-cccc-cccc-cccccccccccc', N'teacher_01', @PasswordHash, 'teacher01@mathinsight.local',  N'Teacher', N'One',     @TeacherRoleId, CAST(1 AS BIT)),
    ('dddddddd-dddd-dddd-dddd-dddddddddddd', N'student_01', @PasswordHash, 'student01@mathinsight.local',  N'Student', N'One',     @StudentRoleId, CAST(1 AS BIT)),
    ('eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', N'student_02', @PasswordHash, 'student02@mathinsight.local',  N'Student', N'Two',     @StudentRoleId, CAST(1 AS BIT))
) AS source ([AccountID], [Username], [PasswordHash], [Email], [FirstName], [LastName], [RoleID], [isActive])
ON target.[Username] = source.[Username]
WHEN MATCHED THEN
    UPDATE SET
        target.[PasswordHash] = source.[PasswordHash],
        target.[Email] = source.[Email],
        target.[FirstName] = source.[FirstName],
        target.[LastName] = source.[LastName],
        target.[RoleID] = source.[RoleID],
        target.[isActive] = source.[isActive]
WHEN NOT MATCHED THEN
    INSERT ([AccountID], [Username], [PasswordHash], [Email], [FirstName], [LastName], [RoleID], [isActive])
    VALUES (source.[AccountID], source.[Username], source.[PasswordHash], source.[Email], source.[FirstName], source.[LastName], source.[RoleID], source.[isActive]);

MERGE [Expert] AS target
USING (VALUES
    ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'Mathematics'),
    ('ffffffff-ffff-ffff-ffff-ffffffffffff', 'Mathematics')
) AS source ([ExpertID], [Specialty])
ON target.[ExpertID] = source.[ExpertID]
WHEN MATCHED THEN
    UPDATE SET target.[Specialty] = source.[Specialty]
WHEN NOT MATCHED THEN
    INSERT ([ExpertID], [Specialty])
    VALUES (source.[ExpertID], source.[Specialty]);

MERGE [Teacher] AS target
USING (VALUES
    ('cccccccc-cccc-cccc-cccc-cccccccccccc', N'Development seed teacher account', CAST(1 AS BIT), '012345678901')
) AS source ([TeacherID], [Biography], [isVerified], [cccd_number])
ON target.[TeacherID] = source.[TeacherID]
WHEN MATCHED THEN
    UPDATE SET
        target.[Biography] = source.[Biography],
        target.[isVerified] = source.[isVerified],
        target.[cccd_number] = source.[cccd_number]
WHEN NOT MATCHED THEN
    INSERT ([TeacherID], [Biography], [isVerified], [cccd_number])
    VALUES (source.[TeacherID], source.[Biography], source.[isVerified], source.[cccd_number]);

MERGE [Student] AS target
USING (VALUES
    ('dddddddd-dddd-dddd-dddd-dddddddddddd', 'Male',   N'MathInsight High School', 12),
    ('eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', 'Female', N'MathInsight High School', 11)
) AS source ([StudentID], [Gender], [School], [CurrentGrade])
ON target.[StudentID] = source.[StudentID]
WHEN MATCHED THEN
    UPDATE SET
        target.[Gender] = source.[Gender],
        target.[School] = source.[School],
        target.[CurrentGrade] = source.[CurrentGrade]
WHEN NOT MATCHED THEN
    INSERT ([StudentID], [Gender], [School], [CurrentGrade])
    VALUES (source.[StudentID], source.[Gender], source.[School], source.[CurrentGrade]);

COMMIT TRANSACTION;

SELECT
    [Username],
    [Email],
    [isActive],
    [RoleID]
FROM [Account]
WHERE [Username] IN (N'admin', N'expert_01', N'expert_02', N'teacher_01', N'student_01', N'student_02')
ORDER BY [Username];

GO

/* ===== SOURCE: 001_QuestionBank_TagTopic_TagDifficulty_Seed.sql ===== */
-- Seed data for QuestionBank topic tags and difficulty levels.
-- Safe to run more than once.

SET XACT_ABORT ON;
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.TagDifficulty', N'U') IS NULL
BEGIN
    THROW 51000, N'Table dbo.TagDifficulty does not exist. Run the schema script before this seed script.', 1;
END;

IF OBJECT_ID(N'dbo.TagTopic', N'U') IS NULL
BEGIN
    THROW 51001, N'Table dbo.TagTopic does not exist. Run the schema script before this seed script.', 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @DifficultySeed TABLE
    (
        DifficultyID VARCHAR(36) NOT NULL,
        DifficultyName NVARCHAR(50) NOT NULL,
        Description NVARCHAR(255) NULL,
        LevelValue INT NOT NULL,
        DisplayOrder INT NOT NULL,
        IsActive BIT NOT NULL
    );

    INSERT INTO @DifficultySeed
        (DifficultyID, DifficultyName, Description, LevelValue, DisplayOrder, IsActive)
    VALUES
        ('DIFF-LEVEL-1', N'Nhận biết', N'Nhận diện khái niệm, công thức hoặc thao tác cơ bản.', 1, 1, 1),
        ('DIFF-LEVEL-2', N'Thông hiểu', N'Hiểu bản chất và áp dụng trực tiếp kiến thức đã học.', 2, 2, 1),
        ('DIFF-LEVEL-3', N'Vận dụng', N'Kết hợp nhiều bước giải hoặc biến đổi quen thuộc.', 3, 3, 1),
        ('DIFF-LEVEL-4', N'Vận dụng cao', N'Câu hỏi phân loại, cần lập luận hoặc biến đổi nâng cao.', 4, 4, 1);

    UPDATE target
    SET
        target.DifficultyName = source.DifficultyName,
        target.Description = source.Description,
        target.LevelValue = source.LevelValue,
        target.DisplayOrder = source.DisplayOrder,
        target.IsActive = source.IsActive
    FROM dbo.TagDifficulty AS target
    INNER JOIN @DifficultySeed AS source
        ON target.DifficultyID = source.DifficultyID
        OR target.DifficultyName = source.DifficultyName
        OR target.LevelValue = source.LevelValue;

    INSERT INTO dbo.TagDifficulty
        (DifficultyID, DifficultyName, Description, LevelValue, DisplayOrder, IsActive)
    SELECT
        source.DifficultyID,
        source.DifficultyName,
        source.Description,
        source.LevelValue,
        source.DisplayOrder,
        source.IsActive
    FROM @DifficultySeed AS source
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.TagDifficulty AS target
        WHERE target.DifficultyID = source.DifficultyID
           OR target.DifficultyName = source.DifficultyName
           OR target.LevelValue = source.LevelValue
    );

    DECLARE @TopicSeed TABLE
    (
        TagID VARCHAR(36) NOT NULL,
        ParentTagName NVARCHAR(50) NULL,
        TagName NVARCHAR(50) NOT NULL,
        Description NVARCHAR(255) NULL,
        Grade INT NOT NULL,
        DisplayOrder INT NOT NULL
    );

    INSERT INTO @TopicSeed
        (TagID, ParentTagName, TagName, Description, Grade, DisplayOrder)
    VALUES
        ('TOPIC-G10-ALG', NULL, N'Lớp 10 - Đại số', N'Nhóm chủ đề đại số lớp 10.', 10, 100),
        ('TOPIC-G10-GEO', NULL, N'Lớp 10 - Hình học', N'Nhóm chủ đề hình học lớp 10.', 10, 110),
        ('TOPIC-G10-STAT', NULL, N'Lớp 10 - Xác suất thống kê', N'Nhóm chủ đề xác suất và thống kê lớp 10.', 10, 120),
        ('TOPIC-G11-ALG', NULL, N'Lớp 11 - Đại số giải tích', N'Nhóm chủ đề đại số và giải tích lớp 11.', 11, 200),
        ('TOPIC-G11-GEO', NULL, N'Lớp 11 - Hình học không gian', N'Nhóm chủ đề hình học không gian lớp 11.', 11, 210),
        ('TOPIC-G11-STAT', NULL, N'Lớp 11 - Xác suất thống kê', N'Nhóm chủ đề xác suất và thống kê lớp 11.', 11, 220),
        ('TOPIC-G12-CALC', NULL, N'Lớp 12 - Giải tích', N'Nhóm chủ đề giải tích lớp 12.', 12, 300),
        ('TOPIC-G12-GEO', NULL, N'Lớp 12 - Hình học Oxyz', N'Nhóm chủ đề hình học không gian và tọa độ Oxyz lớp 12.', 12, 310),
        ('TOPIC-G12-STAT', NULL, N'Lớp 12 - Xác suất thống kê', N'Nhóm chủ đề xác suất và thống kê lớp 12.', 12, 320),

        ('TOPIC-G10-SET', N'Lớp 10 - Đại số', N'Lớp 10 - Mệnh đề, tập hợp', N'Mệnh đề, tập hợp và các phép toán trên tập hợp.', 10, 1),
        ('TOPIC-G10-INEQ', N'Lớp 10 - Đại số', N'Lớp 10 - Bất phương trình', N'Bất đẳng thức, bất phương trình và miền nghiệm.', 10, 2),
        ('TOPIC-G10-QUAD', N'Lớp 10 - Đại số', N'Lớp 10 - Hàm số bậc hai', N'Hàm số bậc hai, đồ thị và ứng dụng.', 10, 3),
        ('TOPIC-G10-EQSYS', N'Lớp 10 - Đại số', N'Lớp 10 - Phương trình, hệ PT', N'Phương trình và hệ phương trình cơ bản.', 10, 4),
        ('TOPIC-G10-VEC', N'Lớp 10 - Hình học', N'Lớp 10 - Vectơ', N'Vectơ và các phép toán với vectơ.', 10, 1),
        ('TOPIC-G10-COORD', N'Lớp 10 - Hình học', N'Lớp 10 - Tọa độ phẳng', N'Tọa độ trong mặt phẳng.', 10, 2),
        ('TOPIC-G10-TRI', N'Lớp 10 - Hình học', N'Lớp 10 - Hệ thức lượng', N'Hệ thức lượng trong tam giác.', 10, 3),
        ('TOPIC-G10-DATA', N'Lớp 10 - Xác suất thống kê', N'Lớp 10 - Thống kê', N'Đọc, mô tả và phân tích dữ liệu thống kê.', 10, 1),
        ('TOPIC-G10-PROB', N'Lớp 10 - Xác suất thống kê', N'Lớp 10 - Xác suất', N'Xác suất cơ bản và biến cố.', 10, 2),

        ('TOPIC-G11-TRIG', N'Lớp 11 - Đại số giải tích', N'Lớp 11 - Lượng giác', N'Hàm số lượng giác và phương trình lượng giác.', 11, 1),
        ('TOPIC-G11-SEQ', N'Lớp 11 - Đại số giải tích', N'Lớp 11 - Dãy số', N'Dãy số và các tính chất cơ bản.', 11, 2),
        ('TOPIC-G11-PROG', N'Lớp 11 - Đại số giải tích', N'Lớp 11 - Cấp số', N'Cấp số cộng và cấp số nhân.', 11, 3),
        ('TOPIC-G11-LIMIT', N'Lớp 11 - Đại số giải tích', N'Lớp 11 - Giới hạn', N'Giới hạn của dãy số và hàm số.', 11, 4),
        ('TOPIC-G11-DERIV', N'Lớp 11 - Đại số giải tích', N'Lớp 11 - Đạo hàm', N'Đạo hàm và các quy tắc tính đạo hàm.', 11, 5),
        ('TOPIC-G11-LINEPLANE', N'Lớp 11 - Hình học không gian', N'Lớp 11 - Đường thẳng, mặt phẳng', N'Đường thẳng, mặt phẳng trong không gian.', 11, 1),
        ('TOPIC-G11-PARALLEL', N'Lớp 11 - Hình học không gian', N'Lớp 11 - Song song', N'Quan hệ song song trong không gian.', 11, 2),
        ('TOPIC-G11-PERP', N'Lớp 11 - Hình học không gian', N'Lớp 11 - Vuông góc', N'Quan hệ vuông góc trong không gian.', 11, 3),
        ('TOPIC-G11-COUNT', N'Lớp 11 - Xác suất thống kê', N'Lớp 11 - Quy tắc đếm', N'Quy tắc cộng, quy tắc nhân và bài toán đếm.', 11, 1),
        ('TOPIC-G11-COMB', N'Lớp 11 - Xác suất thống kê', N'Lớp 11 - Tổ hợp, xác suất', N'Hoán vị, chỉnh hợp, tổ hợp và xác suất.', 11, 2),

        ('TOPIC-G12-DERIVAPP', N'Lớp 12 - Giải tích', N'Lớp 12 - Ứng dụng đạo hàm', N'Khảo sát hàm số, cực trị, GTLN/GTNN và tiệm cận.', 12, 1),
        ('TOPIC-G12-EXPLOG', N'Lớp 12 - Giải tích', N'Lớp 12 - Mũ và logarit', N'Hàm số mũ, logarit và phương trình liên quan.', 12, 2),
        ('TOPIC-G12-INTEGRAL', N'Lớp 12 - Giải tích', N'Lớp 12 - Nguyên hàm, tích phân', N'Nguyên hàm, tích phân và ứng dụng.', 12, 3),
        ('TOPIC-G12-COMPLEX', N'Lớp 12 - Giải tích', N'Lớp 12 - Số phức', N'Số phức và các phép toán liên quan.', 12, 4),
        ('TOPIC-G12-POLY', N'Lớp 12 - Hình học Oxyz', N'Lớp 12 - Khối đa diện', N'Khối đa diện, thể tích và khoảng cách.', 12, 1),
        ('TOPIC-G12-REV', N'Lớp 12 - Hình học Oxyz', N'Lớp 12 - Mặt tròn xoay', N'Mặt nón, mặt trụ, mặt cầu và thể tích.', 12, 2),
        ('TOPIC-G12-OXYZ', N'Lớp 12 - Hình học Oxyz', N'Lớp 12 - Tọa độ Oxyz', N'Tọa độ điểm, vectơ và khoảng cách trong Oxyz.', 12, 3),
        ('TOPIC-G12-LINEPLANE', N'Lớp 12 - Hình học Oxyz', N'Lớp 12 - Mặt phẳng, đường thẳng', N'Phương trình mặt phẳng và đường thẳng trong Oxyz.', 12, 4),
        ('TOPIC-G12-SPHERE', N'Lớp 12 - Hình học Oxyz', N'Lớp 12 - Mặt cầu', N'Phương trình mặt cầu và bài toán liên quan.', 12, 5),
        ('TOPIC-G12-CONPROB', N'Lớp 12 - Xác suất thống kê', N'Lớp 12 - Xác suất có điều kiện', N'Xác suất có điều kiện và công thức Bayes.', 12, 1),
        ('TOPIC-G12-DATA', N'Lớp 12 - Xác suất thống kê', N'Lớp 12 - Thống kê', N'Các bài toán thống kê trong chương trình lớp 12.', 12, 2);

    IF EXISTS
    (
        SELECT 1
        FROM @TopicSeed AS child
        WHERE child.ParentTagName IS NOT NULL
          AND NOT EXISTS
          (
              SELECT 1
              FROM @TopicSeed AS parent
              WHERE parent.TagName = child.ParentTagName
          )
    )
    BEGIN
        THROW 51002, N'Topic seed contains a child row whose parent topic is missing.', 1;
    END;

    UPDATE target
    SET
        target.ParentTagID = NULL,
        target.TagName = source.TagName,
        target.Description = source.Description,
        target.Grade = source.Grade,
        target.DisplayOrder = source.DisplayOrder,
        target.IsActive = 1
    FROM dbo.TagTopic AS target
    INNER JOIN @TopicSeed AS source
        ON target.TagID = source.TagID
        OR target.TagName = source.TagName
    WHERE source.ParentTagName IS NULL;

    INSERT INTO dbo.TagTopic
        (TagID, ParentTagID, TagName, Description, Grade, IsActive, DisplayOrder)
    SELECT
        source.TagID,
        NULL,
        source.TagName,
        source.Description,
        source.Grade,
        1,
        source.DisplayOrder
    FROM @TopicSeed AS source
    WHERE source.ParentTagName IS NULL
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.TagTopic AS target
          WHERE target.TagID = source.TagID
             OR target.TagName = source.TagName
      );

    UPDATE target
    SET
        target.ParentTagID = parent.TagID,
        target.TagName = source.TagName,
        target.Description = source.Description,
        target.Grade = source.Grade,
        target.DisplayOrder = source.DisplayOrder,
        target.IsActive = 1
    FROM dbo.TagTopic AS target
    INNER JOIN @TopicSeed AS source
        ON target.TagID = source.TagID
        OR target.TagName = source.TagName
    INNER JOIN dbo.TagTopic AS parent
        ON parent.TagName = source.ParentTagName
    WHERE source.ParentTagName IS NOT NULL;

    INSERT INTO dbo.TagTopic
        (TagID, ParentTagID, TagName, Description, Grade, IsActive, DisplayOrder)
    SELECT
        source.TagID,
        parent.TagID,
        source.TagName,
        source.Description,
        source.Grade,
        1,
        source.DisplayOrder
    FROM @TopicSeed AS source
    INNER JOIN dbo.TagTopic AS parent
        ON parent.TagName = source.ParentTagName
    WHERE source.ParentTagName IS NOT NULL
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.TagTopic AS target
          WHERE target.TagID = source.TagID
             OR target.TagName = source.TagName
      );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    THROW;
END CATCH;

SELECT
    DifficultyID,
    DifficultyName,
    LevelValue,
    DisplayOrder,
    IsActive
FROM dbo.TagDifficulty
WHERE DifficultyID LIKE 'DIFF-LEVEL-%'
   OR LevelValue BETWEEN 1 AND 4
ORDER BY DisplayOrder;

SELECT
    child.TagID,
    child.ParentTagID,
    parent.TagName AS ParentTagName,
    child.TagName,
    child.Grade,
    child.DisplayOrder,
    child.IsActive
FROM dbo.TagTopic AS child
LEFT JOIN dbo.TagTopic AS parent
    ON parent.TagID = child.ParentTagID
WHERE child.TagID LIKE 'TOPIC-%'
   OR child.TagName LIKE N'Lớp 10 - %'
   OR child.TagName LIKE N'Lớp 11 - %'
   OR child.TagName LIKE N'Lớp 12 - %'
ORDER BY
    child.Grade,
    CASE WHEN child.ParentTagID IS NULL THEN child.DisplayOrder ELSE parent.DisplayOrder END,
    CASE WHEN child.ParentTagID IS NULL THEN 0 ELSE 1 END,
    child.DisplayOrder,
    child.TagName;

GO

/* ===== SOURCE: 002_QuestionBank_SampleQuestion_Seed.sql ===== */
-- Sample question seed for the QuestionBank module.
-- Prerequisite: run 001_QuestionBank_TagTopic_TagDifficulty_Seed.sql first.
-- Safe to run more than once.

SET XACT_ABORT ON;
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.Expert', N'U') IS NULL
BEGIN
    THROW 51100, N'Table dbo.Expert does not exist. Run the schema script before this seed script.', 1;
END;

IF OBJECT_ID(N'dbo.Question', N'U') IS NULL
BEGIN
    THROW 51101, N'Table dbo.Question does not exist. Run the schema script before this seed script.', 1;
END;

IF OBJECT_ID(N'dbo.Answer', N'U') IS NULL
BEGIN
    THROW 51102, N'Table dbo.Answer does not exist. Run the schema script before this seed script.', 1;
END;

IF OBJECT_ID(N'dbo.QuestionTopic', N'U') IS NULL
BEGIN
    THROW 51103, N'Table dbo.QuestionTopic does not exist. Run the schema script before this seed script.', 1;
END;

IF OBJECT_ID(N'dbo.QuestionPart', N'U') IS NULL
BEGIN
    THROW 51104, N'Table dbo.QuestionPart does not exist. Run migration 002 before seeding composite questions.', 1;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Expert)
BEGIN
    THROW 51105, N'No expert found. Create or approve at least one Expert account before seeding questions.', 1;
END;

DECLARE @QuestionTypeConstraint NVARCHAR(MAX) =
(
    SELECT [definition]
    FROM sys.check_constraints
    WHERE [name] = N'CK_Question_QuestionType'
      AND parent_object_id = OBJECT_ID(N'dbo.Question')
);

IF @QuestionTypeConstraint IS NOT NULL
   AND (@QuestionTypeConstraint NOT LIKE '%TrueFalse%' OR @QuestionTypeConstraint NOT LIKE '%Composite%')
BEGIN
    THROW 51106, N'CK_Question_QuestionType does not allow TrueFalse/Composite. Run migration 002 before this seed script.', 1;
END;

DECLARE @StatusConstraint NVARCHAR(MAX) =
(
    SELECT [definition]
    FROM sys.check_constraints
    WHERE [name] = N'CK_Question_Status'
      AND parent_object_id = OBJECT_ID(N'dbo.Question')
);

DECLARE @SupportsPending BIT =
    CASE
        WHEN @StatusConstraint IS NULL THEN 1
        WHEN @StatusConstraint LIKE '%''Pending''%' THEN 1
        ELSE 0
    END;

DECLARE @ExpertID VARCHAR(36);
SELECT TOP (1) @ExpertID = ExpertID
FROM dbo.Expert
ORDER BY ExpertID;

DECLARE @DifficultyLevel1 VARCHAR(36) = 'DIFF-LEVEL-1';
DECLARE @DifficultyLevel2 VARCHAR(36) = 'DIFF-LEVEL-2';
DECLARE @DifficultyLevel3 VARCHAR(36) = 'DIFF-LEVEL-3';

IF NOT EXISTS (SELECT 1 FROM dbo.TagDifficulty WHERE DifficultyID = @DifficultyLevel1)
   OR NOT EXISTS (SELECT 1 FROM dbo.TagDifficulty WHERE DifficultyID = @DifficultyLevel2)
   OR NOT EXISTS (SELECT 1 FROM dbo.TagDifficulty WHERE DifficultyID = @DifficultyLevel3)
BEGIN
    THROW 51107, N'Missing seeded difficulty levels. Run 001_QuestionBank_TagTopic_TagDifficulty_Seed.sql first.', 1;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.TagTopic WHERE TagID = 'TOPIC-G12-DERIVAPP')
   OR NOT EXISTS (SELECT 1 FROM dbo.TagTopic WHERE TagID = 'TOPIC-G12-EXPLOG')
   OR NOT EXISTS (SELECT 1 FROM dbo.TagTopic WHERE TagID = 'TOPIC-G11-DERIV')
   OR NOT EXISTS (SELECT 1 FROM dbo.TagTopic WHERE TagID = 'TOPIC-G10-QUAD')
   OR NOT EXISTS (SELECT 1 FROM dbo.TagTopic WHERE TagID = 'TOPIC-G12-INTEGRAL')
BEGIN
    THROW 51108, N'Missing seeded topic tags. Run 001_QuestionBank_TagTopic_TagDifficulty_Seed.sql first.', 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @QuestionSeed TABLE
    (
        QuestionID VARCHAR(36) NOT NULL,
        QuestionContent NVARCHAR(MAX) NOT NULL,
        SolutionContent NVARCHAR(MAX) NOT NULL,
        PictureUrl VARCHAR(255) NULL,
        DifficultyID VARCHAR(36) NOT NULL,
        Grade INT NOT NULL,
        [Status] VARCHAR(20) NOT NULL,
        QuestionType VARCHAR(30) NOT NULL,
        DefaultPoint DECIMAL(4,2) NOT NULL,
        IsActive BIT NOT NULL
    );

    INSERT INTO @QuestionSeed
        (QuestionID, QuestionContent, SolutionContent, PictureUrl, DifficultyID, Grade, [Status], QuestionType, DefaultPoint, IsActive)
    VALUES
        (
            'Q-SEED-001',
            N'Cho hàm số $y = x^3 - 3x^2 + 1$. Hàm số đạt cực đại tại giá trị nào của $x$?',
            N'Ta có $y^\prime = 3x^2 - 6x = 3x(x-2)$. Dấu của $y^\prime$ đổi từ dương sang âm khi qua $x=0$, nên hàm số đạt cực đại tại $x=0$.',
            NULL,
            @DifficultyLevel2,
            12,
            'Approved',
            'SingleChoice',
            0.20,
            1
        ),
        (
            'Q-SEED-002',
            N'Chọn tất cả khẳng định đúng về hàm số $y = \log_2 x$.',
            N'Với cơ số $2 > 1$, hàm số xác định trên $(0;+\infty)$, đồng biến trên tập xác định và đi qua điểm $(1;0)$.',
            NULL,
            @DifficultyLevel2,
            12,
            'Approved',
            'MultipleChoice',
            0.20,
            1
        ),
        (
            'Q-SEED-003',
            N'Mệnh đề sau đúng hay sai: Nếu $y = x^3 - 2x + 1$ thì $y^\prime = 3x^2 - 2$.',
            N'Áp dụng quy tắc đạo hàm: $(x^3)^\prime = 3x^2$, $(-2x)^\prime = -2$, hằng số có đạo hàm bằng 0. Do đó mệnh đề đúng.',
            NULL,
            @DifficultyLevel1,
            11,
            'Approved',
            'TrueFalse',
            0.20,
            1
        ),
        (
            'Q-SEED-004',
            N'Tìm giá trị nhỏ nhất của hàm số $y = x^2 - 4x + 7$ trên $\mathbb{R}$.',
            N'Ta có $y = (x - 2)^2 + 3$. Vì $(x - 2)^2 \ge 0$, giá trị nhỏ nhất là 3 khi $x = 2$.',
            NULL,
            @DifficultyLevel2,
            10,
            CASE WHEN @SupportsPending = 1 THEN 'Pending' ELSE 'Approved' END,
            'ShortAnswer',
            0.20,
            1
        ),
        (
            'Q-SEED-005',
            N'Cho $F(t)$ là điện năng lưu trữ, $F^\prime(t)=f(t)$, với $f(t)=-0.15t^2+1.8t$, $0\le t\le 12$, và $F(0)=0$. Xét các mệnh đề sau.',
            N'Tích phân $f(t)$ cho ta $F(t)=-0.05t^3+0.9t^2$. Từ đó kiểm tra từng mệnh đề bằng định nghĩa tích phân và thay giá trị cụ thể.',
            NULL,
            @DifficultyLevel3,
            12,
            CASE WHEN @SupportsPending = 1 THEN 'Pending' ELSE 'Approved' END,
            'Composite',
            1.00,
            1
        );

    UPDATE target
    SET
        target.QuestionContent = source.QuestionContent,
        target.SolutionContent = source.SolutionContent,
        target.PictureUrl = source.PictureUrl,
        target.DifficultyID = source.DifficultyID,
        target.Grade = source.Grade,
        target.[Status] = source.[Status],
        target.QuestionType = source.QuestionType,
        target.ExpertID = @ExpertID,
        target.DefaultPoint = source.DefaultPoint,
        target.IsActive = source.IsActive
    FROM dbo.Question AS target
    INNER JOIN @QuestionSeed AS source
        ON source.QuestionID = target.QuestionID;

    INSERT INTO dbo.Question
        (QuestionID, QuestionContent, SolutionContent, PictureUrl, DifficultyID, Grade, [Status], QuestionType, ExpertID, DefaultPoint, IsActive)
    SELECT
        source.QuestionID,
        source.QuestionContent,
        source.SolutionContent,
        source.PictureUrl,
        source.DifficultyID,
        source.Grade,
        source.[Status],
        source.QuestionType,
        @ExpertID,
        source.DefaultPoint,
        source.IsActive
    FROM @QuestionSeed AS source
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.Question AS target
        WHERE target.QuestionID = source.QuestionID
    );

    DECLARE @QuestionTopicSeed TABLE
    (
        QuestionTopicID VARCHAR(36) NOT NULL,
        QuestionID VARCHAR(36) NOT NULL,
        TagID VARCHAR(36) NOT NULL,
        IsPrimary BIT NOT NULL
    );

    INSERT INTO @QuestionTopicSeed
        (QuestionTopicID, QuestionID, TagID, IsPrimary)
    VALUES
        ('QT-SEED-001', 'Q-SEED-001', 'TOPIC-G12-DERIVAPP', 1),
        ('QT-SEED-002', 'Q-SEED-002', 'TOPIC-G12-EXPLOG', 1),
        ('QT-SEED-003', 'Q-SEED-003', 'TOPIC-G11-DERIV', 1),
        ('QT-SEED-004', 'Q-SEED-004', 'TOPIC-G10-QUAD', 1),
        ('QT-SEED-005', 'Q-SEED-005', 'TOPIC-G12-INTEGRAL', 1);

    UPDATE target
    SET
        target.QuestionID = source.QuestionID,
        target.TagID = source.TagID,
        target.IsPrimary = source.IsPrimary
    FROM dbo.QuestionTopic AS target
    INNER JOIN @QuestionTopicSeed AS source
        ON source.QuestionTopicID = target.QuestionTopicID;

    INSERT INTO dbo.QuestionTopic
        (QuestionTopicID, QuestionID, TagID, IsPrimary)
    SELECT
        source.QuestionTopicID,
        source.QuestionID,
        source.TagID,
        source.IsPrimary
    FROM @QuestionTopicSeed AS source
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.QuestionTopic AS target
        WHERE target.QuestionTopicID = source.QuestionTopicID
    );

    DECLARE @AnswerSeed TABLE
    (
        AnswerID VARCHAR(36) NOT NULL,
        QuestionID VARCHAR(36) NOT NULL,
        AnswerContent NVARCHAR(MAX) NOT NULL,
        IsCorrect BIT NOT NULL
    );

    INSERT INTO @AnswerSeed
        (AnswerID, QuestionID, AnswerContent, IsCorrect)
    VALUES
        ('A-SEED-001-A', 'Q-SEED-001', N'$x=0$', 1),
        ('A-SEED-001-B', 'Q-SEED-001', N'$x=2$', 0),
        ('A-SEED-001-C', 'Q-SEED-001', N'$x=1$', 0),
        ('A-SEED-001-D', 'Q-SEED-001', N'$x=-1$', 0),

        ('A-SEED-002-A', 'Q-SEED-002', N'Tập xác định là $(0;+\infty)$.', 1),
        ('A-SEED-002-B', 'Q-SEED-002', N'Hàm số đồng biến trên tập xác định.', 1),
        ('A-SEED-002-C', 'Q-SEED-002', N'Đồ thị đi qua điểm $(1;0)$.', 1),
        ('A-SEED-002-D', 'Q-SEED-002', N'Hàm số xác định với mọi $x \in \mathbb{R}$.', 0),

        ('A-SEED-003-A', 'Q-SEED-003', N'Đúng', 1),
        ('A-SEED-003-B', 'Q-SEED-003', N'Sai', 0),

        ('A-SEED-004-A', 'Q-SEED-004', N'3', 1);

    UPDATE target
    SET
        target.QuestionID = source.QuestionID,
        target.AnswerContent = source.AnswerContent,
        target.IsCorrect = source.IsCorrect
    FROM dbo.Answer AS target
    INNER JOIN @AnswerSeed AS source
        ON source.AnswerID = target.AnswerID;

    INSERT INTO dbo.Answer
        (AnswerID, QuestionID, AnswerContent, IsCorrect)
    SELECT
        source.AnswerID,
        source.QuestionID,
        source.AnswerContent,
        source.IsCorrect
    FROM @AnswerSeed AS source
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.Answer AS target
        WHERE target.AnswerID = source.AnswerID
    );

    DECLARE @QuestionPartSeed TABLE
    (
        PartID VARCHAR(36) NOT NULL,
        QuestionID VARCHAR(36) NOT NULL,
        PartOrder INT NOT NULL,
        PartLabel NVARCHAR(10) NULL,
        PartContent NVARCHAR(MAX) NOT NULL,
        PartType VARCHAR(30) NOT NULL,
        CorrectBoolean BIT NULL,
        CorrectText NVARCHAR(255) NULL,
        CorrectNumeric DECIMAL(18,6) NULL,
        NumericTolerance DECIMAL(18,6) NULL,
        Explanation NVARCHAR(MAX) NULL,
        DefaultPoint DECIMAL(4,2) NOT NULL
    );

    INSERT INTO @QuestionPartSeed
        (PartID, QuestionID, PartOrder, PartLabel, PartContent, PartType, CorrectBoolean, CorrectText, CorrectNumeric, NumericTolerance, Explanation, DefaultPoint)
    VALUES
        (
            'QP-SEED-005-A',
            'Q-SEED-005',
            1,
            N'a',
            N'$F(t)=-0.05t^3+0.9t^2$, với $0\le t\le 12$.',
            'TrueFalse',
            1,
            NULL,
            NULL,
            NULL,
            N'Lấy nguyên hàm của $f(t)$, dùng $F(0)=0$ để suy ra hằng số bằng 0.',
            0.25
        ),
        (
            'QP-SEED-005-B',
            'Q-SEED-005',
            2,
            N'b',
            N'Điện năng lưu trữ từ thời điểm $t=a$ đến $t=b$ là $\int_a^b f(t)\,dt$.',
            'TrueFalse',
            1,
            NULL,
            NULL,
            NULL,
            N'Tốc độ lưu trữ là đạo hàm $F^\prime(t)=f(t)$, nên độ tăng của $F$ trên đoạn là tích phân của $f$.',
            0.25
        ),
        (
            'QP-SEED-005-C',
            'Q-SEED-005',
            3,
            N'c',
            N'Điện năng lưu trữ từ $t=1$ đến $t=5$ nhỏ hơn 15.3 kWh.',
            'TrueFalse',
            0,
            NULL,
            NULL,
            NULL,
            N'Tính $F(5)-F(1)$ và so sánh với 15.3.',
            0.25
        ),
        (
            'QP-SEED-005-D',
            'Q-SEED-005',
            4,
            N'd',
            N'Giá trị $F(5)-F(1)$ bằng bao nhiêu kWh?',
            'NumericAnswer',
            NULL,
            NULL,
            17.600000,
            0.010000,
            N'Từ $F(t)=-0.05t^3+0.9t^2$, ta có $F(5)-F(1)=17.6$.',
            0.25
        );

    UPDATE target
    SET
        target.QuestionID = source.QuestionID,
        target.PartOrder = source.PartOrder,
        target.PartLabel = source.PartLabel,
        target.PartContent = source.PartContent,
        target.PartType = source.PartType,
        target.CorrectBoolean = source.CorrectBoolean,
        target.CorrectText = source.CorrectText,
        target.CorrectNumeric = source.CorrectNumeric,
        target.NumericTolerance = source.NumericTolerance,
        target.Explanation = source.Explanation,
        target.DefaultPoint = source.DefaultPoint
    FROM dbo.QuestionPart AS target
    INNER JOIN @QuestionPartSeed AS source
        ON source.PartID = target.PartID;

    INSERT INTO dbo.QuestionPart
        (PartID, QuestionID, PartOrder, PartLabel, PartContent, PartType, CorrectBoolean, CorrectText, CorrectNumeric, NumericTolerance, Explanation, DefaultPoint)
    SELECT
        source.PartID,
        source.QuestionID,
        source.PartOrder,
        source.PartLabel,
        source.PartContent,
        source.PartType,
        source.CorrectBoolean,
        source.CorrectText,
        source.CorrectNumeric,
        source.NumericTolerance,
        source.Explanation,
        source.DefaultPoint
    FROM @QuestionPartSeed AS source
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.QuestionPart AS target
        WHERE target.PartID = source.PartID
    );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    THROW;
END CATCH;

SELECT
    CASE
        WHEN @SupportsPending = 1
            THEN N'Seeded 3 Approved questions and 2 Pending questions.'
        ELSE N'CK_Question_Status does not allow Pending, so all 5 sample questions were seeded as Approved.'
    END AS SeedStatusNote;

SELECT
    question.QuestionID,
    question.[Status],
    question.QuestionType,
    question.Grade,
    difficulty.DifficultyName,
    topic.TagName AS PrimaryTopic,
    question.DefaultPoint,
    question.IsActive
FROM dbo.Question AS question
INNER JOIN dbo.TagDifficulty AS difficulty
    ON difficulty.DifficultyID = question.DifficultyID
INNER JOIN dbo.QuestionTopic AS questionTopic
    ON questionTopic.QuestionID = question.QuestionID
   AND questionTopic.IsPrimary = 1
INNER JOIN dbo.TagTopic AS topic
    ON topic.TagID = questionTopic.TagID
WHERE question.QuestionID IN ('Q-SEED-001', 'Q-SEED-002', 'Q-SEED-003', 'Q-SEED-004', 'Q-SEED-005')
ORDER BY question.QuestionID;

SELECT
    part.QuestionID,
    part.PartID,
    part.PartOrder,
    part.PartLabel,
    part.PartType,
    part.CorrectBoolean,
    part.CorrectText,
    part.CorrectNumeric,
    part.NumericTolerance,
    part.DefaultPoint
FROM dbo.QuestionPart AS part
WHERE part.QuestionID = 'Q-SEED-005'
ORDER BY part.PartOrder;
GO

/* ===== Lecture demo seed (teacher_01) =====
   Runs after the TagTopic seed above, since Lecture.TagID is a FK into TagTopic.
   Ties every row to teacher_01 (cccccccc-cccc-cccc-cccc-cccccccccccc) so the
   Teacher and Student lecture screens have something to render out of the box.
*/
BEGIN TRANSACTION;

MERGE [Lecture] AS target
USING (VALUES
    ('11111111-1111-1111-1111-111111111101', N'Mệnh đề và tập hợp', N'Bài giảng giới thiệu mệnh đề, tập hợp và các phép toán trên tập hợp.', N'cccccccc-cccc-cccc-cccc-cccccccccccc', N'TOPIC-G10-SET', N'Published'),
    ('11111111-1111-1111-1111-111111111102', N'Dãy số và cấp số', N'Bài giảng về dãy số, cấp số cộng và cấp số nhân.', N'cccccccc-cccc-cccc-cccc-cccccccccccc', N'TOPIC-G11-SEQ', N'Published'),
    ('11111111-1111-1111-1111-111111111103', N'Ứng dụng đạo hàm khảo sát hàm số', N'Bài giảng đang soạn về khảo sát hàm số bằng đạo hàm.', N'cccccccc-cccc-cccc-cccc-cccccccccccc', N'TOPIC-G12-DERIVAPP', N'Draft')
) AS source ([LectureID], [Title], [Content], [TeacherID], [TagID], [Status])
ON target.[LectureID] = source.[LectureID]
WHEN MATCHED THEN
    UPDATE SET
        target.[Title] = source.[Title],
        target.[Content] = source.[Content],
        target.[TeacherID] = source.[TeacherID],
        target.[TagID] = source.[TagID],
        target.[Status] = source.[Status]
WHEN NOT MATCHED THEN
    INSERT ([LectureID], [Title], [Content], [TeacherID], [TagID], [Status])
    VALUES (source.[LectureID], source.[Title], source.[Content], source.[TeacherID], source.[TagID], source.[Status]);

COMMIT TRANSACTION;
GO

GO
