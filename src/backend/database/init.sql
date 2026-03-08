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

UPDATE dbo.Tasks
SET Status = CASE
    WHEN Status IN ('Created', 'Planned') THEN 'Todo'
    WHEN Status IN ('InProgress', 'Blocked') THEN 'Doing'
    WHEN Status = 'Archived' THEN 'Done'
    ELSE Status
END
WHERE Status IN ('Created', 'Planned', 'InProgress', 'Blocked', 'Archived');
GO

IF EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = 'CK_Tasks_Status'
      AND parent_object_id = OBJECT_ID('dbo.Tasks')
)
BEGIN
    ALTER TABLE dbo.Tasks DROP CONSTRAINT CK_Tasks_Status;
END
GO

ALTER TABLE dbo.Tasks
ADD CONSTRAINT CK_Tasks_Status CHECK (Status IN ('Todo', 'Doing', 'Done'));
GO
