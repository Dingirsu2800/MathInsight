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
