-- Run Session A first, then run Session B immediately in a second SSMS tab.
-- Use a disposable test database. The script only takes a lock; it does not change data.
-- Replace the value with an existing QuestionID in that database.

-- Session A
DECLARE @QuestionId VARCHAR(36) = 'REPLACE-WITH-QUESTION-ID';

SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;

SELECT 1
FROM dbo.Question WITH (UPDLOCK, HOLDLOCK)
WHERE QuestionID = @QuestionId;

WAITFOR DELAY '00:00:10';
ROLLBACK TRANSACTION;

-- Session B
-- DECLARE @QuestionId VARCHAR(36) = 'REPLACE-WITH-QUESTION-ID';
-- DECLARE @StartedAt DATETIME2(7) = SYSUTCDATETIME();
--
-- SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
-- BEGIN TRANSACTION;
--
-- SELECT 1
-- FROM dbo.Question WITH (UPDLOCK, HOLDLOCK)
-- WHERE QuestionID = @QuestionId;
--
-- SELECT DATEDIFF(MILLISECOND, @StartedAt, SYSUTCDATETIME()) AS WaitMilliseconds;
-- ROLLBACK TRANSACTION;
--
-- Expected: Session B blocks until Session A rolls back, then WaitMilliseconds is about 10,000.
