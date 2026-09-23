IF COL_LENGTH('dbo.Blueprint', 'CreatedTime') IS NULL
BEGIN
    ALTER TABLE dbo.Blueprint ADD CreatedTime DATETIME2(0) NULL;
END;
GO

IF COL_LENGTH('dbo.Notification', 'Content') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Notification ALTER COLUMN Content NVARCHAR(2500) NOT NULL;
END;
GO
