IF DB_ID('TaskTrackerDb') IS NULL
BEGIN
    CREATE DATABASE TaskTrackerDb;
END
GO

USE TaskTrackerDb;
GO

IF OBJECT_ID('dbo.Tasks', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Tasks
    (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        Title NVARCHAR(200) NOT NULL,
        Description NVARCHAR(1000) NOT NULL,
        Status NVARCHAR(20) NOT NULL,
        DueDate DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL,
        CONSTRAINT CK_Tasks_Status CHECK (Status IN ('Todo', 'Doing', 'Done'))
    );
END
GO
