-- Widen TeacherApplication.DocumentsUrl so a teacher application can reference multiple
-- certificate images (BR-05). The column holds the URLs newline-separated; a single
-- Cloudinary URL is ~125 characters, so the previous VARCHAR(255) fit only one.
--
-- Widening is non-destructive: existing single-URL rows are unaffected.
IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[TeacherApplication]')
      AND name = N'DocumentsUrl'
      AND max_length < 2000
)
BEGIN
    ALTER TABLE [TeacherApplication]
        ALTER COLUMN [DocumentsUrl] VARCHAR(2000) NOT NULL;
END
GO
