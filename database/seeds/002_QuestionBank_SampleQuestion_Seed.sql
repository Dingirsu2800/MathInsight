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
