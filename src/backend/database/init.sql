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
        Priority NVARCHAR(20) NOT NULL CONSTRAINT DF_Tasks_Priority DEFAULT 'Medium',
        TargetStartDate DATETIME2 NULL,
        TargetDueDate DATETIME2 NULL,
        DueDate DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL,
        CONSTRAINT CK_Tasks_Status CHECK (Status IN ('Todo', 'Doing', 'Done')),
        CONSTRAINT CK_Tasks_Priority CHECK (Priority IN ('Low', 'Medium', 'High', 'Critical'))
    );
END
GO

IF COL_LENGTH('dbo.Tasks', 'Priority') IS NULL
BEGIN
    ALTER TABLE dbo.Tasks
    ADD Priority NVARCHAR(20) NOT NULL CONSTRAINT DF_Tasks_Priority DEFAULT 'Medium';
END
GO

IF COL_LENGTH('dbo.Tasks', 'TargetStartDate') IS NULL
BEGIN
    ALTER TABLE dbo.Tasks
    ADD TargetStartDate DATETIME2 NULL;
END
GO

IF COL_LENGTH('dbo.Tasks', 'TargetDueDate') IS NULL
BEGIN
    ALTER TABLE dbo.Tasks
    ADD TargetDueDate DATETIME2 NULL;
END
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
    WHERE name = 'CK_Tasks_Priority'
      AND parent_object_id = OBJECT_ID('dbo.Tasks')
)
BEGIN
    ALTER TABLE dbo.Tasks DROP CONSTRAINT CK_Tasks_Priority;
END
GO

ALTER TABLE dbo.Tasks
ADD CONSTRAINT CK_Tasks_Status CHECK (Status IN ('Todo', 'Doing', 'Done'));
GO

ALTER TABLE dbo.Tasks
ADD CONSTRAINT CK_Tasks_Priority CHECK (Priority IN ('Low', 'Medium', 'High', 'Critical'));
GO

IF OBJECT_ID('dbo.TaskLabels', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TaskLabels
    (
        TaskId UNIQUEIDENTIFIER NOT NULL,
        Label NVARCHAR(30) NOT NULL,
        CONSTRAINT PK_TaskLabels PRIMARY KEY (TaskId, Label),
        CONSTRAINT FK_TaskLabels_Task FOREIGN KEY (TaskId)
            REFERENCES dbo.Tasks (Id) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID('dbo.TaskComments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TaskComments
    (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TaskId UNIQUEIDENTIFIER NOT NULL,
        Content NVARCHAR(1000) NOT NULL,
        ImageDataUrl NVARCHAR(MAX) NULL,
        ImageFileName NVARCHAR(260) NULL,
        CreatedAt DATETIME2 NOT NULL,
        CONSTRAINT FK_TaskComments_Task FOREIGN KEY (TaskId)
            REFERENCES dbo.Tasks (Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_TaskComments_TaskId_CreatedAt
        ON dbo.TaskComments (TaskId, CreatedAt);
END
GO

IF COL_LENGTH('dbo.TaskComments', 'ImageDataUrl') IS NULL
BEGIN
    ALTER TABLE dbo.TaskComments
    ADD ImageDataUrl NVARCHAR(MAX) NULL;
END
GO

IF COL_LENGTH('dbo.TaskComments', 'ImageFileName') IS NULL
BEGIN
    ALTER TABLE dbo.TaskComments
    ADD ImageFileName NVARCHAR(260) NULL;
END
GO

IF OBJECT_ID('dbo.TaskActivity', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TaskActivity
    (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TaskId UNIQUEIDENTIFIER NOT NULL,
        Action NVARCHAR(80) NOT NULL,
        Detail NVARCHAR(1000) NOT NULL,
        CreatedAt DATETIME2 NOT NULL
    );

    CREATE INDEX IX_TaskActivity_TaskId_CreatedAt
        ON dbo.TaskActivity (TaskId, CreatedAt DESC);
END
GO
